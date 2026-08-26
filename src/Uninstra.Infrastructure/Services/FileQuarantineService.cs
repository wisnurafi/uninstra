namespace Uninstra.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Results;
using Uninstra.Core.Safety;

/// <summary>
/// File-based quarantine. Items are MOVED (not copied) into
/// %LOCALAPPDATA%/Uninstra/Quarantine/&lt;operationId&gt;/ together with a JSON manifest,
/// enabling full restore. Manifests are the single source of truth for listing.
/// </summary>
public sealed class FileQuarantineService : IQuarantineService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<FileQuarantineService> _logger;
    private readonly string _quarantineRoot;
    private readonly int _retentionDays;

    public FileQuarantineService(ISettingsService settingsService, ILogger<FileQuarantineService> logger)
    {
        _logger = logger;
        var settings = settingsService.Load();
        _retentionDays = settings.QuarantineRetentionDays;

        _quarantineRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Uninstra", "Quarantine");
        Directory.CreateDirectory(_quarantineRoot);
    }

    public string QuarantineRoot => _quarantineRoot;

    public async Task<OperationResult> MoveToQuarantineAsync(
        LeftoverCandidate item, string operationId, string appName, CancellationToken ct = default)
    {
        // Only filesystem items can be quarantined
        if (item.Type is not (LeftoverType.File or LeftoverType.Directory
            or LeftoverType.Shortcut or LeftoverType.EmptyDirectory))
        {
            return OperationResult.Failure("NOT_QUARANTINABLE",
                $"{item.Type} items cannot be quarantined");
        }

        if (string.IsNullOrWhiteSpace(item.Path) || SafetyPolicy.ContainsPathTraversal(item.Path))
            return OperationResult.Failure("INVALID_PATH", "Item path is invalid");

        try
        {
            var opDir = Path.Combine(_quarantineRoot, operationId);
            Directory.CreateDirectory(opDir);

            var fileName = Path.GetFileName(item.Path.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(fileName)) fileName = "unnamed";
            var destPath = Path.Combine(opDir, $"{item.Id}_{fileName}");

            long size = 0;
            if (File.Exists(item.Path))
            {
                size = new FileInfo(item.Path).Length;
                File.SetAttributes(item.Path, FileAttributes.Normal);
                File.Move(item.Path, destPath, overwrite: true);
            }
            else if (Directory.Exists(item.Path))
            {
                size = await Task.Run(() =>
                    EnumerateSizeSafe(item.Path), ct).ConfigureAwait(false);
                Directory.Move(item.Path, destPath);
            }
            else
            {
                return OperationResult.Failure("NOT_FOUND", "Source no longer exists");
            }

            var manifest = new QuarantineManifest
            {
                OperationId = operationId,
                ApplicationId = item.ApplicationId,
                ApplicationName = appName,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = _retentionDays < 0
                    ? DateTime.MaxValue
                    : DateTime.UtcNow.AddDays(_retentionDays),
                OriginalPath = item.Path,
                QuarantinePath = destPath,
                Size = size,
                Hash = await ComputeHashAsync(destPath, File.Exists(destPath), ct).ConfigureAwait(false),
                ItemType = item.Type,
                CanRestore = true
            };

            await File.WriteAllTextAsync(
                Path.Combine(opDir, $"{item.Id}.manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions), ct).ConfigureAwait(false);

            _logger.LogInformation("Quarantined {Path} -> {Dest} ({Size} bytes)",
                item.Path, destPath, size);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to quarantine {Path}", item.Path);
            return OperationResult.Failure("QUARANTINE_FAILED", ex.Message);
        }
    }

    public async Task<OperationResult> RestoreAsync(QuarantineManifest manifest, CancellationToken ct = default)
    {
        try
        {
            // Normalize FIRST: scanner-provided paths may carry trailing
            // separators, which breaks GetDirectoryName (it returns the path
            // itself) and makes Directory.Move fail with "destination already
            // exists" against a folder that does not really exist yet.
            var originalPath = manifest.OriginalPath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var quarantinePath = manifest.QuarantinePath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Safety rail: never write anywhere via traversal paths
            if (string.IsNullOrWhiteSpace(originalPath) ||
                SafetyPolicy.ContainsPathTraversal(originalPath))
                return OperationResult.Failure("INVALID_PATH", "Original path is invalid");

            var warnings = new List<string>();

            if (File.Exists(quarantinePath))
            {
                var targetDir = Path.GetDirectoryName(originalPath);
                if (!string.IsNullOrEmpty(targetDir))
                    Directory.CreateDirectory(targetDir);

                if (File.Exists(originalPath))
                {
                    warnings.Add($"Existing file kept: {originalPath}");
                }
                else
                {
                    File.SetAttributes(quarantinePath, FileAttributes.Normal);
                    File.Move(quarantinePath, originalPath, overwrite: false);
                }
            }
            else if (Directory.Exists(quarantinePath))
            {
                var parentDir = Path.GetDirectoryName(originalPath);
                if (!string.IsNullOrEmpty(parentDir))
                    Directory.CreateDirectory(parentDir);

                if (!Directory.Exists(originalPath))
                {
                    Directory.Move(quarantinePath, originalPath);
                }
                else
                {
                    // Destination already exists (e.g. partially restored
                    // before): MERGE children, skipping collisions, so one
                    // conflicting file cannot fail the whole restore.
                    foreach (var child in Directory.EnumerateFileSystemEntries(quarantinePath))
                    {
                        var target = Path.Combine(originalPath, Path.GetFileName(child));
                        try
                        {
                            if (Directory.Exists(target) || File.Exists(target))
                            {
                                warnings.Add($"Skipped (already exists): {target}");
                                continue;
                            }

                            if (Directory.Exists(child)) Directory.Move(child, target);
                            else
                            {
                                File.SetAttributes(child, FileAttributes.Normal);
                                File.Move(child, target);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Restore merge skipped {Child}", child);
                            warnings.Add($"Skipped (error): {target}");
                        }
                    }

                    // Remove the now-drained quarantine folder (best-effort)
                    try { if (!Directory.EnumerateFileSystemEntries(quarantinePath).Any())
                            Directory.Delete(quarantinePath); }
                    catch { /* leftover empty dir is harmless */ }
                }
            }
            else
            {
                return OperationResult.Failure("NOT_FOUND",
                    "Quarantined item no longer exists — it may have been permanently deleted");
            }

            RemoveManifest(manifest);
            _logger.LogInformation("Restored {Quarantine} -> {Original} (warnings: {Count})",
                quarantinePath, originalPath, warnings.Count);
            return await Task.FromResult(OperationResult.Success(
                warnings.Count > 0 ? [.. warnings] : [])).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore {Quarantine}", manifest.QuarantinePath);
            return OperationResult.Failure("RESTORE_FAILED", ex.Message);
        }
    }

    public async Task<OperationResult> PermanentDeleteAsync(
        QuarantineManifest manifest, CancellationToken ct = default)
    {
        try
        {
            // Hard safety rail: only ever delete INSIDE the quarantine root
            var normalizedRoot = Path.GetFullPath(_quarantineRoot)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedTarget = Path.GetFullPath(manifest.QuarantinePath);
            if (!normalizedTarget.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "Refused permanent delete outside quarantine root: {Path}", normalizedTarget);
                return OperationResult.Failure("PROTECTED_PATH",
                    "Refusing to delete outside the quarantine area");
            }

            if (File.Exists(manifest.QuarantinePath))
            {
                File.SetAttributes(manifest.QuarantinePath, FileAttributes.Normal);
                File.Delete(manifest.QuarantinePath);
            }
            else if (Directory.Exists(manifest.QuarantinePath))
            {
                Directory.Delete(manifest.QuarantinePath, recursive: true);
            }

            RemoveManifest(manifest);
            _logger.LogInformation("Permanently deleted {Quarantine}", manifest.QuarantinePath);
            return await Task.FromResult(OperationResult.Success()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to permanently delete {Quarantine}", manifest.QuarantinePath);
            return OperationResult.Failure("DELETE_FAILED", ex.Message);
        }
    }

    public Task<IReadOnlyList<QuarantineManifest>> GetAllAsync(CancellationToken ct = default)
    {
        var manifests = new List<QuarantineManifest>();
        try
        {
            foreach (var manifestFile in Directory.EnumerateFiles(
                _quarantineRoot, "*.manifest.json", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var json = File.ReadAllText(manifestFile);
                    var m = JsonSerializer.Deserialize<QuarantineManifest>(json, JsonOptions);
                    if (m is not null) manifests.Add(m);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Corrupt quarantine manifest: {File}", manifestFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed enumerating quarantine manifests");
        }

        return Task.FromResult<IReadOnlyList<QuarantineManifest>>(
            manifests.OrderByDescending(m => m.CreatedAt).ToList());
    }

    public async Task CleanExpiredAsync(CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        foreach (var m in all.Where(m => m.ExpiresAt != DateTime.MaxValue && m.ExpiresAt < now))
        {
            _logger.LogInformation("Auto-purging expired quarantine item from {CreatedAt}", m.CreatedAt);
            await PermanentDeleteAsync(m, ct).ConfigureAwait(false);
        }
    }

    private void RemoveManifest(QuarantineManifest manifest)
    {
        try
        {
            var manifestFile = Path.Combine(
                Path.GetDirectoryName(manifest.QuarantinePath) ?? _quarantineRoot,
                $"{ManifestIdFromPath(manifest)}.manifest.json");

            // Fallback: search by content match when id layout changed
            if (!File.Exists(manifestFile))
            {
                manifestFile = Directory.EnumerateFiles(
                    _quarantineRoot, "*.manifest.json", SearchOption.AllDirectories)
                    .FirstOrDefault(f =>
                    {
                        try { return File.ReadAllText(f).Contains(manifest.OperationId, StringComparison.Ordinal); }
                        catch { return false; }
                    }) ?? manifestFile;
            }

            if (File.Exists(manifestFile))
                File.Delete(manifestFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove manifest for {Op}", manifest.OperationId);
        }
    }

    private static string ManifestIdFromPath(QuarantineManifest manifest)
    {
        var name = Path.GetFileName(manifest.QuarantinePath);
        var idx = name.IndexOf('_', StringComparison.Ordinal);
        return idx > 0 ? name[..idx] : manifest.ApplicationId;
    }

    private static long EnumerateSizeSafe(string dir)
    {
        long size = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { size += new FileInfo(f).Length; } catch { /* access denied */ }
        }
        return size;
    }

    private static async Task<string> ComputeHashAsync(string path, bool isFile, CancellationToken ct)
    {
        if (!isFile) return string.Empty;
        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(fs, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }
}
