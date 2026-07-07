namespace Uninstra.Windows.Scanning;

using Microsoft.Extensions.Logging;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Results;
using Uninstra.Core.Safety;

public sealed class JunkScannerService : IJunkScanner
{
    private readonly ILogger<JunkScannerService> _logger;

    public JunkScannerService(ILogger<JunkScannerService> logger) => _logger = logger;

    public Task<IReadOnlyList<JunkCategory>> ScanAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var categories = new List<JunkCategory>();

            // User Temp
            categories.Add(ScanTempFolder(
                "user-temp", "User Temp Files",
                "Temporary files from user temp folder",
                Path.GetTempPath(), ct));

            // Windows Temp (if accessible)
            var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            if (Directory.Exists(winTemp))
            {
                categories.Add(ScanTempFolder(
                    "windows-temp", "Windows Temp Files",
                    "Temporary files from Windows temp folder (accessible files only)",
                    winTemp, ct, requiresElevation: true));
            }

            // Crash Dumps
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var crashDumps = Path.Combine(localAppData, "CrashDumps");
            if (Directory.Exists(crashDumps))
            {
                categories.Add(ScanTempFolder(
                    "crash-dumps", "Application Crash Dumps",
                    "Crash dump files from application errors",
                    crashDumps, ct));
            }

            // Thumbnail cache
            var thumbCache = Path.Combine(localAppData, @"Microsoft\Windows\Explorer");
            if (Directory.Exists(thumbCache))
            {
                categories.Add(ScanFilesWithPattern(
                    "thumb-cache", "Thumbnail Cache",
                    "Windows Explorer thumbnail cache files",
                    thumbCache, "thumbcache_*.db", ct));
            }

            categories.RemoveAll(c => c.ItemCount == 0);

            return (IReadOnlyList<JunkCategory>)categories;
        }, ct);
    }

    public async Task<OperationResult> CleanAsync(IReadOnlyList<JunkItem> items, CancellationToken ct = default)
    {
        int cleaned = 0, failed = 0;

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var (isProtected, _) = SafetyPolicy.EvaluatePath(item.Path);
            if (isProtected) { failed++; continue; }

            try
            {
                if (item.IsLocked) { failed++; continue; }

                if (File.Exists(item.Path))
                    File.Delete(item.Path);
                else if (Directory.Exists(item.Path))
                    Directory.Delete(item.Path, true);

                cleaned++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to clean: {Path}", item.Path);
                failed++;
            }
        }

        return cleaned > 0
            ? OperationResult.Success()
            : OperationResult.Failure("CLEAN_FAILED", $"Failed to clean {failed} items");
    }

    private JunkCategory ScanTempFolder(string id, string name, string description,
        string path, CancellationToken ct, bool requiresElevation = false)
    {
        var items = new List<JunkItem>();
        long totalSize = 0;

        try
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(file);
                        var isLocked = IsFileLocked(file);
                        items.Add(new JunkItem
                        {
                            Id = Guid.NewGuid().ToString("N")[..16],
                            Path = file,
                            Size = fi.Length,
                            LastModified = fi.LastWriteTime,
                            IsLocked = isLocked,
                            CategoryId = id
                        });
                        totalSize += fi.Length;
                    }
                    catch { /* skip inaccessible files */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning temp folder: {Path}", path);
        }

        return new JunkCategory
        {
            Id = id,
            Name = name,
            Description = description,
            ItemCount = items.Count,
            DetectedSize = totalSize,
            Risk = RiskLevel.Low,
            RequiresElevation = requiresElevation,
            Items = items
        };
    }

    private JunkCategory ScanFilesWithPattern(string id, string name, string description,
        string path, string pattern, CancellationToken ct)
    {
        var items = new List<JunkItem>();
        long totalSize = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, pattern))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(file);
                    items.Add(new JunkItem
                    {
                        Id = Guid.NewGuid().ToString("N")[..16],
                        Path = file,
                        Size = fi.Length,
                        LastModified = fi.LastWriteTime,
                        IsLocked = IsFileLocked(file),
                        CategoryId = id
                    });
                    totalSize += fi.Length;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning: {Path}/{Pattern}", path, pattern);
        }

        return new JunkCategory
        {
            Id = id,
            Name = name,
            Description = description,
            ItemCount = items.Count,
            DetectedSize = totalSize,
            Risk = RiskLevel.Low,
            Items = items
        };
    }

    private static bool IsFileLocked(string path)
    {
        try
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException) { return true; }
        catch { return false; }
    }
}
