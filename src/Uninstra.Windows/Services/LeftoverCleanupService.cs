namespace Uninstra.Windows.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Results;
using Uninstra.Core.Safety;

/// <summary>
/// Executes cleanup. Order of protection per item:
/// 1) protected-path refusal, 2) quarantine move (filesystem items),
/// 3) registry backup (.reg export before any key/value deletion),
/// 4) deletion — elevated items routed through the helper process.
/// </summary>
public sealed class LeftoverCleanupService : ILeftoverCleanupService
{
    private readonly ILogger<LeftoverCleanupService> _logger;
    private readonly IQuarantineService _quarantine;
    private readonly RegistryBackupService _registryBackup;
    private readonly IElevatedHelperClient _elevatedClient;

    public LeftoverCleanupService(
        ILogger<LeftoverCleanupService> logger,
        IQuarantineService quarantine,
        RegistryBackupService registryBackup,
        IElevatedHelperClient elevatedClient)
    {
        _logger = logger;
        _quarantine = quarantine;
        _registryBackup = registryBackup;
        _elevatedClient = elevatedClient;
    }

    public async Task<OperationResult<CleanupSummary>> CleanAsync(
        IReadOnlyList<LeftoverCandidate> items,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken ct = default,
        string? applicationDisplayName = null)
    {
        int cleaned = 0, failed = 0, skipped = 0, quarantined = 0;
        long freedBytes = 0;

        var operationId = Guid.NewGuid().ToString("N")[..16];
        string? backupPath = null;

        // Pre-flight: back up every registry item ONCE before anything is deleted.
        // Best-effort: a failed .reg export must not abort the whole cleanup —
        // every deletion target is already validated (SafetyPolicy + helper-side
        // rails), so we log the miss and continue without the backup file.
        var regItems = items.Where(i =>
            i.Type is LeftoverType.RegistryKey or LeftoverType.RegistryValue or LeftoverType.StartupEntry).ToList();
        if (regItems.Count > 0)
        {
            try
            {
                backupPath = _registryBackup.BackupRegistryItems(regItems, operationId);
                if (backupPath is not null)
                    progress?.Report(new CleanupProgress(
                        $"Registry backup saved ({Path.GetFileName(backupPath)})", 0, items.Count, "backup"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Registry backup failed before cleanup ({Count} items) — continuing without a .reg fallback",
                    regItems.Count);
                progress?.Report(new CleanupProgress(
                    "Registry backup unavailable — continuing", 0, items.Count, "backup"));
            }
        }

        var appName = string.IsNullOrWhiteSpace(applicationDisplayName)
            ? items.FirstOrDefault()?.ApplicationId ?? "unknown"
            : applicationDisplayName;

        for (int i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = items[i];

            progress?.Report(new CleanupProgress(item.DisplayName, i + 1, items.Count, "Cleaning..."));

            // Safety check
            if (item.IsProtected)
            {
                _logger.LogWarning("Skipping protected item: {Item}", item.DisplayName);
                skipped++;
                continue;
            }

            try
            {
                var outcome = item.Type switch
                {
                    LeftoverType.File or LeftoverType.Shortcut or LeftoverType.EmptyDirectory or LeftoverType.Directory
                        => await CleanFileSystemAsync(item, operationId, appName, ct).ConfigureAwait(false),
                    LeftoverType.RegistryKey => await CleanRegistryKeyAsync(item, ct).ConfigureAwait(false),
                    LeftoverType.RegistryValue => await CleanRegistryValueAsync(item, ct).ConfigureAwait(false),
                    LeftoverType.StartupEntry => await CleanRegistryValueAsync(item, ct).ConfigureAwait(false),
                    LeftoverType.ScheduledTask => await CleanScheduledTaskAsync(item, ct).ConfigureAwait(false),
                    LeftoverType.Service => await CleanServiceAsync(item, ct).ConfigureAwait(false),
                    _ => ItemOutcome.Failed
                };

                switch (outcome)
                {
                    case ItemOutcome.Cleaned:
                        cleaned++;
                        freedBytes += item.SizeBytes;
                        break;
                    case ItemOutcome.Quarantined:
                        quarantined++;
                        freedBytes += item.SizeBytes;
                        break;
                    case ItemOutcome.Skipped:
                        skipped++;
                        break;
                    default:
                        failed++;
                        _logger.LogWarning("Failed to clean: {Type} {Path}",
                            item.Type, item.Path ?? item.RegistryPath);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Error cleaning {Item}", item.DisplayName);
            }
        }

        progress?.Report(new CleanupProgress("Done", items.Count, items.Count, "Cleanup complete"));

        var summary = new CleanupSummary(items.Count, cleaned, failed, skipped, freedBytes);
        return OperationResult.Success(summary);
    }

    /// <summary>Total bytes recoverable by restoring everything currently quarantined under an operation.</summary>
    public static string DescribeQuarantine(IReadOnlyList<QuarantineManifest> manifests) =>
        manifests.Count == 0 ? string.Empty : $"{manifests.Count} item(s) restorable";

    // ────────────────────────────────────────────────────────────────

    private async Task<ItemOutcome> CleanFileSystemAsync(
        LeftoverCandidate item, string operationId, string appId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.Path)) return ItemOutcome.Failed;

        var (isProtected, _) = SafetyPolicy.EvaluatePath(item.Path);
        if (isProtected) return ItemOutcome.Skipped;

        var gone = !File.Exists(item.Path) && !Directory.Exists(item.Path);
        if (gone) return ItemOutcome.Cleaned; // already gone

        if (item.RequiresElevation && !HasWriteAccessToParent(item.Path))
        {
            // Route the delete through the elevated helper as a quarantine move.
            var qRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Uninstra", "Quarantine", operationId);
            var destName = Path.GetFileName(item.Path.TrimEnd(Path.DirectorySeparatorChar));
            var payload = $"{item.Path}|{Path.Combine(qRoot, $"{item.Id}_{destName}")}";

            var response = await _elevatedClient.ExecuteAsync(
                ElevatedOperationType.MoveToQuarantine, payload, ct).ConfigureAwait(false);

            return response.Success ? ItemOutcome.Quarantined : ItemOutcome.Failed;
        }

        // In-process path: quarantine first (reversible), fall back to direct delete
        var result = await _quarantine.MoveToQuarantineAsync(
            item, operationId, appId, ct).ConfigureAwait(false);

        if (result.IsSuccess) return ItemOutcome.Quarantined;

        _logger.LogDebug("Quarantine unavailable ({Code}), deleting directly", result.Error?.Code);
        return DeleteDirectly(item.Path);
    }

    private async Task<ItemOutcome> CleanRegistryKeyAsync(LeftoverCandidate item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.RegistryPath)) return ItemOutcome.Failed;

        try
        {
            if (item.RequiresElevation)
            {
                var response = await _elevatedClient.ExecuteAsync(
                    ElevatedOperationType.DeleteRegistryKey,
                    $"{item.RegistryPath}|{item.RegistryValueName}",
                    ct).ConfigureAwait(false);
                if (response.Success) return ItemOutcome.Cleaned;
                _logger.LogDebug("Elevated key delete declined: {Msg}", response.Message);
                // fall through to in-process attempt (may still work if user has rights)
            }

            var hive = item.RegistryHive == RegistryHiveType.LocalMachine
                ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            var normalizedPath = item.RegistryPath.Replace('/', '\\');
            var parentPath = RegistryParent(normalizedPath);
            var keyName = RegistryLeaf(normalizedPath);

            if (parentPath is null || keyName is null || keyName.Length == 0) return ItemOutcome.Failed;

            using var parent = baseKey.OpenSubKey(parentPath, writable: true);
            if (parent is null) return ItemOutcome.Cleaned; // already gone

            parent.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
            return ItemOutcome.Cleaned;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied deleting registry key: {Path}", item.RegistryPath);
            return ItemOutcome.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting registry key: {Path}", item.RegistryPath);
            return ItemOutcome.Failed;
        }
    }

    private async Task<ItemOutcome> CleanRegistryValueAsync(LeftoverCandidate item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.RegistryPath)) return ItemOutcome.Failed;
        if (string.IsNullOrEmpty(item.RegistryValueName))
            return await CleanRegistryKeyAsync(item, ct).ConfigureAwait(false); // fallback to key deletion

        try
        {
            if (item.RequiresElevation)
            {
                var response = await _elevatedClient.ExecuteAsync(
                    ElevatedOperationType.DeleteRegistryValue,
                    $"{item.RegistryPath}|{item.RegistryValueName}",
                    ct).ConfigureAwait(false);
                if (response.Success) return ItemOutcome.Cleaned;
                _logger.LogDebug("Elevated value delete declined: {Msg}", response.Message);
            }

            var hive = item.RegistryHive == RegistryHiveType.LocalMachine
                ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(item.RegistryPath, writable: true);
            if (key is null) return ItemOutcome.Cleaned;

            key.DeleteValue(item.RegistryValueName, throwOnMissingValue: false);
            return ItemOutcome.Cleaned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting registry value: {Path}\\{Value}",
                item.RegistryPath, item.RegistryValueName);
            return ItemOutcome.Failed;
        }
    }

    private async Task<ItemOutcome> CleanScheduledTaskAsync(LeftoverCandidate item, CancellationToken ct)
    {
        var taskName = item.RegistryValueName; // schtasks /TN path stored here by the scanner
        if (string.IsNullOrEmpty(taskName))
            taskName = item.Path;
        if (string.IsNullOrEmpty(taskName)) return ItemOutcome.Failed;

        var response = await _elevatedClient.ExecuteAsync(
            ElevatedOperationType.DeleteScheduledTask, taskName, ct).ConfigureAwait(false);

        if (response.Success) return ItemOutcome.Cleaned;

        // Fallback to local schtasks (works for user-context tasks without elevation)
        return await Task.Run(() =>
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Delete /TN \"{taskName}\" /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(10000);
                return proc?.ExitCode == 0 ? ItemOutcome.Cleaned : ItemOutcome.Failed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting scheduled task: {Task}", taskName);
                return ItemOutcome.Failed;
            }
        }, ct).ConfigureAwait(false);
    }

    private async Task<ItemOutcome> CleanServiceAsync(LeftoverCandidate item, CancellationToken ct)
    {
        // Extract service short name from the registry path tail
        var serviceName = RegistryLeaf((item.RegistryPath ?? "").Replace('/', '\\'));
        if (string.IsNullOrEmpty(serviceName)) return ItemOutcome.Failed;

        var stopResponse = await _elevatedClient.ExecuteAsync(
            ElevatedOperationType.StopService, serviceName, ct).ConfigureAwait(false);

        var delResponse = await _elevatedClient.ExecuteAsync(
            ElevatedOperationType.DeleteService, serviceName, ct).ConfigureAwait(false);

        if (delResponse.Success) return ItemOutcome.Cleaned;

        _logger.LogWarning("Service delete not confirmed ({Code}): {Msg}",
            delResponse.ErrorCode, delResponse.Message);
        return ItemOutcome.Failed;
    }

    private static ItemOutcome DeleteDirectly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return ItemOutcome.Cleaned;
            }
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(path, recursive: true);
                return ItemOutcome.Cleaned;
            }
            return ItemOutcome.Cleaned;
        }
        catch (Exception)
        {
            return ItemOutcome.Failed;
        }
    }

    private static string? RegistryParent(string path)
    {
        var idx = path.LastIndexOf('\\');
        return idx <= 0 ? null : path[..idx];
    }

    private static string RegistryLeaf(string path)
    {
        var idx = path.LastIndexOf('\\');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    private static bool HasWriteAccessToParent(string path)
    {
        try
        {
            var probeDir = File.Exists(path)
                ? Path.GetDirectoryName(path) ?? path
                : path;
            var testFile = Path.Combine(probeDir, $".uninstra_test_{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            return true;
        }
        catch { return false; }
    }

    private enum ItemOutcome { Cleaned, Quarantined, Skipped, Failed }
}
