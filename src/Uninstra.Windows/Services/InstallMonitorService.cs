namespace Uninstra.Windows.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.ServiceProcess;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;
using Uninstra.Core.Results;

public sealed class InstallMonitorService : IInstallMonitorService
{
    private readonly ILogger<InstallMonitorService> _logger;
    private readonly List<InstallMonitorSession> _sessions = [];
    private readonly object _lock = new();

    public InstallMonitorService(ILogger<InstallMonitorService> logger)
    {
        _logger = logger;
    }

    public async Task<OperationResult<InstallMonitorSession>> StartMonitoringAsync(
        string installerPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            // Validate installer exists
            if (!File.Exists(installerPath))
            {
                return OperationResult<InstallMonitorSession>.Failure(
                    "INSTALLER_NOT_FOUND",
                    "Installer file not found",
                    $"Path: {installerPath}");
            }

            progress?.Report("Creating pre-install snapshot...");
            _logger.LogInformation("Starting install monitoring for: {InstallerPath}", installerPath);

            // Take pre-install snapshot
            var preSnapshot = await TakeSystemSnapshotAsync(progress, ct);
            
            var sessionId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var installerHash = await ComputeFileHashAsync(installerPath, ct);
            var publisher = GetFilePublisher(installerPath);

            progress?.Report("Starting installer...");

            // Run the installer and monitor process tree
            var processResult = await RunInstallerWithMonitoringAsync(installerPath, progress, ct);

            if (!processResult.IsSuccess)
            {
                return OperationResult<InstallMonitorSession>.Failure(
                    "INSTALLER_FAILED",
                    processResult.Error?.Message ?? "Installer execution failed",
                    processResult.Error?.TechnicalDetails);
            }

            progress?.Report("Creating post-install snapshot...");

            // Wait a bit for system to settle
            await Task.Delay(2000, ct);

            // Take post-install snapshot
            var postSnapshot = await TakeSystemSnapshotAsync(progress, ct);

            progress?.Report("Analyzing changes...");

            // Compare snapshots
            var session = CompareSnapshots(
                sessionId,
                installerPath,
                installerHash,
                publisher,
                preSnapshot,
                postSnapshot,
                processResult.Value!);

            lock (_lock)
            {
                _sessions.Add(session);
            }

            _logger.LogInformation(
                "Install monitoring completed. Created: {CreatedFiles}, Modified: {ModifiedFiles}, Registry: {RegistryChanges}",
                session.CreatedFiles.Count,
                session.ModifiedFiles.Count,
                session.RegistryChanges.Count);

            progress?.Report($"Monitoring complete. {session.CreatedFiles.Count} new files, {session.RegistryChanges.Count} registry changes.");

            return OperationResult<InstallMonitorSession>.Success(session);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<InstallMonitorSession>.Failure(
                "CANCELLED",
                "Installation monitoring was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to monitor installation");
            return OperationResult<InstallMonitorSession>.Failure(
                "MONITORING_ERROR",
                "Failed to monitor installation",
                ex.Message);
        }
    }

    public Task<IReadOnlyList<InstallMonitorSession>> GetSessionsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<InstallMonitorSession>>(_sessions.AsReadOnly());
        }
    }

    public Task<InstallMonitorSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_sessions.FirstOrDefault(s => s.SessionId == sessionId));
        }
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (session is not null)
            {
                _sessions.Remove(session);
            }
        }
        return Task.CompletedTask;
    }

    private async Task<SystemSnapshot> TakeSystemSnapshotAsync(
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var snapshot = new SystemSnapshot();

        // Scan installed programs from registry
        progress?.Report("Scanning installed programs...");
        snapshot.InstalledPrograms = GetInstalledPrograms();

        // Scan common installation directories
        progress?.Report("Scanning program files...");
        snapshot.ProgramFiles = await ScanDirectoryAsync(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            ct);
        snapshot.ProgramFilesX86 = await ScanDirectoryAsync(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            ct);

        // Scan AppData folders
        progress?.Report("Scanning AppData...");
        snapshot.LocalAppData = await ScanDirectoryAsync(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ct);

        // Scan registry
        progress?.Report("Scanning registry...");
        snapshot.RegistryKeys = GetRegistrySnapshot();

        // Scan services
        progress?.Report("Scanning services...");
        snapshot.Services = GetServicesSnapshot();

        // Scan scheduled tasks (simplified)
        snapshot.ScheduledTasks = GetScheduledTasksSnapshot();

        // Scan startup entries
        snapshot.StartupEntries = GetStartupEntriesSnapshot();

        return snapshot;
    }

    private HashSet<string> GetInstalledPrograms()
    {
        var programs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Uninstall keys to check
        var keys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var keyPath in keys)
        {
            using var rootKey = Registry.LocalMachine.OpenSubKey(keyPath);
            if (rootKey is null) continue;

            foreach (var subKeyName in rootKey.GetSubKeyNames())
            {
                using var subKey = rootKey.OpenSubKey(subKeyName);
                if (subKey is null) continue;

                var displayName = subKey.GetValue("DisplayName") as string;
                if (!string.IsNullOrEmpty(displayName))
                {
                    programs.Add(displayName);
                }
            }
        }

        return programs;
    }

    private async Task<HashSet<string>> ScanDirectoryAsync(string path, CancellationToken ct)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!Directory.Exists(path))
                return files;

            // Only scan top 2 levels for performance
            await Task.Run(() =>
            {
                try
                {
                    var dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
                    foreach (var dir in dirs.Take(100)) // Limit for performance
                    {
                        ct.ThrowIfCancellationRequested();
                        files.Add(dir);

                        try
                        {
                            var subDirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly);
                            foreach (var subDir in subDirs.Take(20))
                            {
                                files.Add(subDir);
                            }
                        }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan directory: {Path}", path);
        }

        return files;
    }

    private HashSet<string> GetRegistrySnapshot()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Key paths to monitor
        var paths = new[]
        {
            @"SOFTWARE",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
        };

        foreach (var path in paths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key is null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    keys.Add($"{path}\\{subKeyName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read registry path: {Path}", path);
            }

            // Also check CurrentUser
            try
            {
                using var userKey = Registry.CurrentUser.OpenSubKey(path);
                if (userKey is null) continue;

                foreach (var subKeyName in userKey.GetSubKeyNames())
                {
                    keys.Add($"HKCU\\{path}\\{subKeyName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read registry path: {Path}", path);
            }
        }

        return keys;
    }

    private HashSet<string> GetServicesSnapshot()
    {
        var services = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var service in ServiceController.GetServices())
            {
                services.Add(service.ServiceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get services snapshot");
        }

        return services;
    }

    private HashSet<string> GetScheduledTasksSnapshot()
    {
        // Simplified - would need TaskScheduler COM interop for full implementation
        var tasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var taskPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Tasks");
            if (Directory.Exists(taskPath))
            {
                foreach (var file in Directory.GetFiles(taskPath, "*", SearchOption.AllDirectories))
                {
                    tasks.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get scheduled tasks snapshot");
        }

        return tasks;
    }

    private HashSet<string> GetStartupEntriesSnapshot()
    {
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Registry startup
        var runKeys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
        };

        foreach (var keyPath in runKeys)
        {
            try
            {
                using var lmKey = Registry.LocalMachine.OpenSubKey(keyPath);
                if (lmKey is not null)
                {
                    foreach (var name in lmKey.GetValueNames())
                    {
                        entries.Add($"HKLM\\{keyPath}\\{name}");
                    }
                }

                using var cuKey = Registry.CurrentUser.OpenSubKey(keyPath);
                if (cuKey is not null)
                {
                    foreach (var name in cuKey.GetValueNames())
                    {
                        entries.Add($"HKCU\\{keyPath}\\{name}");
                    }
                }
            }
            catch { }
        }

        // Startup folder
        var startupFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\StartUp")
        };

        foreach (var folder in startupFolders)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        entries.Add(Path.GetFileName(file));
                    }
                }
            }
            catch { }
        }

        return entries;
    }

    private async Task<OperationResult<ProcessInfo>> RunInstallerWithMonitoringAsync(
        string installerPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        try
        {
            var extension = Path.GetExtension(installerPath).ToLowerInvariant();
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };

            // For MSI, use msiexec
            if (extension == ".msi")
            {
                startInfo.FileName = "msiexec.exe";
                startInfo.Arguments = $"/i \"{installerPath}\"";
                startInfo.UseShellExecute = false;
            }

            progress?.Report($"Running installer: {Path.GetFileName(installerPath)}");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return OperationResult<ProcessInfo>.Failure(
                    "PROCESS_START_FAILED",
                    "Failed to start installer process");
            }

            var rootPid = process.Id;
            var childPids = new List<int>();
            var startTime = DateTime.UtcNow;

            // Monitor process tree
            while (!process.HasExited && !ct.IsCancellationRequested)
            {
                await Task.Delay(500, ct);
                
                // Find child processes
                try
                {
                    var children = FindChildProcesses(rootPid);
                    foreach (var child in children)
                    {
                        if (!childPids.Contains(child))
                        {
                            childPids.Add(child);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error finding child processes");
                }

                progress?.Report($"Installer running (PID: {rootPid})...");
            }

            var exitCode = process.HasExited ? process.ExitCode : -1;
            var endTime = DateTime.UtcNow;

            return OperationResult<ProcessInfo>.Success(new ProcessInfo(
                rootPid,
                childPids,
                startTime,
                endTime,
                exitCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run installer");
            return OperationResult<ProcessInfo>.Failure(
                "INSTALLER_ERROR",
                "Failed to run installer",
                ex.Message);
        }
    }

    private List<int> FindChildProcesses(int parentPid)
    {
        var children = new List<int>();

        try
        {
            // Use WMI to find child processes
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentPid}");

            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                var childPid = Convert.ToInt32(obj["ProcessId"]);
                children.Add(childPid);

                // Recursively find grandchildren
                children.AddRange(FindChildProcesses(childPid));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to find child processes for PID {Pid}", parentPid);
        }

        return children;
    }

    private InstallMonitorSession CompareSnapshots(
        string sessionId,
        string installerPath,
        string installerHash,
        string publisher,
        SystemSnapshot pre,
        SystemSnapshot post,
        ProcessInfo processInfo)
    {
        var createdFiles = new List<string>();
        var modifiedFiles = new List<string>();
        var createdDirs = new List<string>();
        var registryChanges = new List<string>();
        var newServices = new List<string>();
        var newScheduledTasks = new List<string>();
        var newStartupEntries = new List<string>();
        var detectedApps = new List<string>();
        var warnings = new List<string>();

        // Compare program files
        foreach (var dir in post.ProgramFiles)
        {
            if (!pre.ProgramFiles.Contains(dir))
            {
                createdDirs.Add(dir);
            }
        }

        foreach (var dir in post.ProgramFilesX86)
        {
            if (!pre.ProgramFilesX86.Contains(dir))
            {
                createdDirs.Add(dir);
            }
        }

        foreach (var dir in post.LocalAppData)
        {
            if (!pre.LocalAppData.Contains(dir))
            {
                createdDirs.Add(dir);
            }
        }

        // Compare registry
        foreach (var key in post.RegistryKeys)
        {
            if (!pre.RegistryKeys.Contains(key))
            {
                registryChanges.Add(key);
            }
        }

        // Compare services
        foreach (var service in post.Services)
        {
            if (!pre.Services.Contains(service))
            {
                newServices.Add(service);
            }
        }

        // Compare scheduled tasks
        foreach (var task in post.ScheduledTasks)
        {
            if (!pre.ScheduledTasks.Contains(task))
            {
                newScheduledTasks.Add(task);
            }
        }

        // Compare startup entries
        foreach (var entry in post.StartupEntries)
        {
            if (!pre.StartupEntries.Contains(entry))
            {
                newStartupEntries.Add(entry);
            }
        }

        // Compare installed programs
        foreach (var program in post.InstalledPrograms)
        {
            if (!pre.InstalledPrograms.Contains(program))
            {
                detectedApps.Add(program);
            }
        }

        // Add warnings for suspicious behavior
        if (newServices.Count > 5)
        {
            warnings.Add($"High number of new services installed ({newServices.Count})");
        }

        if (newStartupEntries.Count > 3)
        {
            warnings.Add($"Multiple startup entries added ({newStartupEntries.Count})");
        }

        return new InstallMonitorSession
        {
            SessionId = sessionId,
            InstallerPath = installerPath,
            InstallerHash = installerHash,
            InstallerPublisher = publisher,
            StartedAt = processInfo.StartTime,
            CompletedAt = processInfo.EndTime,
            RootProcessId = processInfo.ProcessId,
            ChildProcesses = processInfo.ChildProcesses,
            CreatedFiles = createdFiles,
            ModifiedFiles = modifiedFiles,
            CreatedDirectories = createdDirs,
            RegistryChanges = registryChanges,
            NewServices = newServices,
            NewScheduledTasks = newScheduledTasks,
            NewStartupEntries = newStartupEntries,
            DetectedApplications = detectedApps,
            Warnings = warnings
        };
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(hash)[..16];
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    private static string GetFilePublisher(string filePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(filePath);
            return info.CompanyName ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private sealed record SystemSnapshot
    {
        public HashSet<string> InstalledPrograms { get; set; } = [];
        public HashSet<string> ProgramFiles { get; set; } = [];
        public HashSet<string> ProgramFilesX86 { get; set; } = [];
        public HashSet<string> LocalAppData { get; set; } = [];
        public HashSet<string> RegistryKeys { get; set; } = [];
        public HashSet<string> Services { get; set; } = [];
        public HashSet<string> ScheduledTasks { get; set; } = [];
        public HashSet<string> StartupEntries { get; set; } = [];
    }

    private sealed record ProcessInfo(
        int ProcessId,
        List<int> ChildProcesses,
        DateTime StartTime,
        DateTime EndTime,
        int ExitCode);
}
