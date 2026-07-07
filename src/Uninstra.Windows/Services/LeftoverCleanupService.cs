namespace Uninstra.Windows.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Results;
using Uninstra.Core.Safety;

public sealed class LeftoverCleanupService : ILeftoverCleanupService
{
    private readonly ILogger<LeftoverCleanupService> _logger;

    public LeftoverCleanupService(ILogger<LeftoverCleanupService> logger) => _logger = logger;

    public async Task<OperationResult<CleanupSummary>> CleanAsync(
        IReadOnlyList<LeftoverCandidate> items,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken ct = default)
    {
        int cleaned = 0, failed = 0, skipped = 0;
        long freedBytes = 0;

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
                var result = item.Type switch
                {
                    LeftoverType.File => await CleanFileAsync(item),
                    LeftoverType.Directory or LeftoverType.EmptyDirectory => await CleanDirectoryAsync(item),
                    LeftoverType.RegistryKey => CleanRegistryKey(item),
                    LeftoverType.RegistryValue => CleanRegistryValue(item),
                    LeftoverType.Shortcut => await CleanFileAsync(item),
                    LeftoverType.StartupEntry => CleanRegistryValue(item),
                    LeftoverType.ScheduledTask => CleanScheduledTask(item),
                    LeftoverType.Service => false, // services require elevation — skip
                    _ => false
                };

                if (result)
                {
                    cleaned++;
                    freedBytes += item.SizeBytes;
                    _logger.LogInformation("Cleaned: {Type} {Path}", item.Type, item.Path ?? item.RegistryPath);
                }
                else
                {
                    failed++;
                    _logger.LogWarning("Failed to clean: {Type} {Path}", item.Type, item.Path ?? item.RegistryPath);
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Error cleaning {Item}", item.DisplayName);
            }
        }

        progress?.Report(new CleanupProgress("Done", items.Count, items.Count, "Cleanup complete"));

        return OperationResult.Success(new CleanupSummary(items.Count, cleaned, failed, skipped, freedBytes));
    }

    private Task<bool> CleanFileAsync(LeftoverCandidate item)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrEmpty(item.Path)) return false;

            var (isProtected, _) = SafetyPolicy.EvaluatePath(item.Path);
            if (isProtected) return false;

            if (!File.Exists(item.Path)) return true; // already gone

            try
            {
                File.SetAttributes(item.Path, FileAttributes.Normal);
                File.Delete(item.Path);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Access denied deleting file: {Path}", item.Path);
                return false;
            }
        });
    }

    private Task<bool> CleanDirectoryAsync(LeftoverCandidate item)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrEmpty(item.Path)) return false;

            var (isProtected, _) = SafetyPolicy.EvaluatePath(item.Path);
            if (isProtected) return false;

            if (!Directory.Exists(item.Path)) return true; // already gone

            try
            {
                // Remove read-only attributes recursively
                foreach (var file in Directory.EnumerateFiles(item.Path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(item.Path, recursive: true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete directory: {Path}", item.Path);
                // Try individual file deletion
                try
                {
                    foreach (var file in Directory.EnumerateFiles(item.Path, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    // Try removing empty dirs bottom-up
                    foreach (var dir in Directory.GetDirectories(item.Path, "*", SearchOption.AllDirectories)
                        .OrderByDescending(d => d.Length))
                    {
                        try { Directory.Delete(dir); } catch { }
                    }
                    try { Directory.Delete(item.Path); } catch { }
                    return !Directory.Exists(item.Path);
                }
                catch { return false; }
            }
        });
    }

    private bool CleanRegistryKey(LeftoverCandidate item)
    {
        if (string.IsNullOrEmpty(item.RegistryPath)) return false;

        try
        {
            var hive = item.RegistryHive == RegistryHiveType.LocalMachine
                ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            var parentPath = Path.GetDirectoryName(item.RegistryPath.Replace('/', '\\'))?.Replace('\\', '\\');
            var keyName = Path.GetFileName(item.RegistryPath.Replace('/', '\\'));

            if (parentPath is null || keyName is null) return false;

            using var parent = baseKey.OpenSubKey(parentPath, writable: true);
            if (parent is null) return true; // parent already gone

            parent.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied deleting registry key: {Path}", item.RegistryPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting registry key: {Path}", item.RegistryPath);
            return false;
        }
    }

    private bool CleanRegistryValue(LeftoverCandidate item)
    {
        if (string.IsNullOrEmpty(item.RegistryPath) || string.IsNullOrEmpty(item.RegistryValueName))
            return CleanRegistryKey(item); // fallback to key deletion

        try
        {
            var hive = item.RegistryHive == RegistryHiveType.LocalMachine
                ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(item.RegistryPath, writable: true);
            if (key is null) return true;

            key.DeleteValue(item.RegistryValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting registry value: {Path}\\{Value}", item.RegistryPath, item.RegistryValueName);
            return false;
        }
    }

    private bool CleanScheduledTask(LeftoverCandidate item)
    {
        if (string.IsNullOrEmpty(item.Path)) return false;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN \"{item.Path}\" /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(10000);
            return proc?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting scheduled task: {Task}", item.Path);
            return false;
        }
    }
}
