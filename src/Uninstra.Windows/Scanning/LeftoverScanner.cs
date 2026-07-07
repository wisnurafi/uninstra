namespace Uninstra.Windows.Scanning;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Safety;
using Uninstra.Core.Scoring;
using Uninstra.Core.Validation;

public sealed class LeftoverScanner : ILeftoverScanner
{
    private readonly ILogger<LeftoverScanner> _logger;

    public LeftoverScanner(ILogger<LeftoverScanner> logger) => _logger = logger;

    public Task<IReadOnlyList<LeftoverCandidate>> ScanAsync(InstalledApplication app, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var candidates = new List<LeftoverCandidate>();

            // Scan install location
            if (!string.IsNullOrWhiteSpace(app.InstallLocation))
                ScanDirectory(app, app.InstallLocation, true, candidates, ct);

            // Scan AppData locations
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var programData = Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData";

            foreach (var baseDir in new[] { localAppData, roamingAppData, programData })
            {
                ct.ThrowIfCancellationRequested();
                ScanForAppFolder(app, baseDir, candidates, ct);
            }

            // Scan registry leftovers
            ScanRegistryLeftovers(app, candidates, ct);

            // Scan shortcuts
            ScanShortcuts(app, candidates, ct);

            // Scan startup entries
            ScanStartupEntries(app, candidates, ct);

            return (IReadOnlyList<LeftoverCandidate>)candidates;
        }, ct);
    }

    private void ScanDirectory(InstalledApplication app, string dirPath, bool isInstallLocation,
        List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        var (isProtected, reason) = SafetyPolicy.EvaluatePath(dirPath);
        if (isProtected) return;

        try
        {
            if (!Directory.Exists(dirPath)) return;

            var dir = new DirectoryInfo(dirPath);
            var isEmpty = !dir.EnumerateFileSystemInfos().Any();

            var ctx = new ScoringContext
            {
                IsExactInstallLocation = isInstallLocation,
                FolderNameMatchesExactly = dir.Name.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                                          dir.Name.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase),
                PublisherAndNameMatch = !string.IsNullOrEmpty(app.Publisher) &&
                    dir.FullName.Contains(app.Publisher.Split(' ')[0], StringComparison.OrdinalIgnoreCase),
                IsProtectedDirectory = isProtected,
                NameVeryShort = app.NormalizedName.Length < 3,
                NameTooGeneric = NameNormalizer.IsGenericName(app.NormalizedName)
            };

            var (score, level, evidence) = ConfidenceScorer.Calculate(ctx);

            long size = 0;
            try { size = dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
            catch { /* access denied */ }

            candidates.Add(new LeftoverCandidate
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                ApplicationId = app.Id,
                DisplayName = dir.Name,
                Type = isEmpty ? LeftoverType.EmptyDirectory : LeftoverType.Directory,
                Path = dirPath,
                SizeBytes = size,
                ConfidenceScore = score,
                ConfidenceLevel = level,
                RiskLevel = level == ConfidenceLevel.High ? RiskLevel.Low : RiskLevel.Medium,
                Evidence = evidence,
                IsSelectedByDefault = ConfidenceScorer.ShouldAutoSelect(score, level, isProtected),
                RequiresElevation = !HasWriteAccess(dirPath),
                IsProtected = isProtected,
                ProtectionReason = reason,
                LastModified = dir.LastWriteTime,
                SourceScanner = "FileSystem"
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning directory: {Path}", dirPath);
        }
    }

    private void ScanForAppFolder(InstalledApplication app, string baseDir,
        List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir)) return;

        try
        {
            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                ct.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(dir);
                if (dirName.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(app.Publisher) && dirName.Equals(app.Publisher, StringComparison.OrdinalIgnoreCase)))
                {
                    ScanDirectory(app, dir, false, candidates, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning base dir: {Dir}", baseDir);
        }
    }

    private void ScanRegistryLeftovers(InstalledApplication app, List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        // Check if the uninstall registry entry still exists
        try
        {
            var hive = app.RegistryHive == RegistryHiveType.LocalMachine
                ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            var view = app.RegistryView == RegistryViewType.Registry32
                ? RegistryView.Registry32 : RegistryView.Registry64;

            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(app.RegistryKeyPath);
            if (key is not null)
            {
                candidates.Add(new LeftoverCandidate
                {
                    Id = Guid.NewGuid().ToString("N")[..16],
                    ApplicationId = app.Id,
                    DisplayName = $"Registry: {app.RegistryKeyPath}",
                    Type = LeftoverType.RegistryKey,
                    RegistryHive = app.RegistryHive,
                    RegistryPath = app.RegistryKeyPath,
                    ConfidenceScore = 95,
                    ConfidenceLevel = ConfidenceLevel.High,
                    RiskLevel = RiskLevel.Low,
                    Evidence = ["Uninstall registry entry still present"],
                    IsSelectedByDefault = true,
                    RequiresElevation = app.RegistryHive == RegistryHiveType.LocalMachine,
                    SourceScanner = "Registry"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning registry leftovers for {App}", app.DisplayName);
        }

        // Scan HKCU\Software for app-specific keys
        ScanRegistrySoftwareKeys(app, candidates, ct);
    }

    private void ScanRegistrySoftwareKeys(InstalledApplication app, List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        try
        {
            using var softwareKey = Registry.CurrentUser.OpenSubKey("Software");
            if (softwareKey is null) return;

            foreach (var subKeyName in softwareKey.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                if (subKeyName.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                    subKeyName.Equals(app.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    if (SafetyPolicy.IsProtectedApplication(subKeyName)) continue;

                    candidates.Add(new LeftoverCandidate
                    {
                        Id = Guid.NewGuid().ToString("N")[..16],
                        ApplicationId = app.Id,
                        DisplayName = $"Registry: HKCU\\Software\\{subKeyName}",
                        Type = LeftoverType.RegistryKey,
                        RegistryHive = RegistryHiveType.CurrentUser,
                        RegistryPath = $"Software\\{subKeyName}",
                        ConfidenceScore = 70,
                        ConfidenceLevel = ConfidenceLevel.Medium,
                        RiskLevel = RiskLevel.Medium,
                        Evidence = [$"Registry key name matches: {subKeyName}"],
                        IsSelectedByDefault = false,
                        SourceScanner = "Registry"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning HKCU\\Software for {App}", app.DisplayName);
        }
    }

    private void ScanShortcuts(InstalledApplication app, List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        var shortcutDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        foreach (var dir in shortcutDirs)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

            try
            {
                foreach (var lnk in Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories))
                {
                    var lnkName = Path.GetFileNameWithoutExtension(lnk);
                    if (lnkName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                        lnkName.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(new LeftoverCandidate
                        {
                            Id = Guid.NewGuid().ToString("N")[..16],
                            ApplicationId = app.Id,
                            DisplayName = $"Shortcut: {lnkName}",
                            Type = LeftoverType.Shortcut,
                            Path = lnk,
                            ConfidenceScore = 80,
                            ConfidenceLevel = ConfidenceLevel.Medium,
                            RiskLevel = RiskLevel.Low,
                            Evidence = [$"Shortcut name matches: {lnkName}"],
                            IsSelectedByDefault = false,
                            SourceScanner = "Shortcuts"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error scanning shortcuts in {Dir}", dir);
            }
        }
    }

    private void ScanStartupEntries(InstalledApplication app, List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (runKey is null) return;

            foreach (var valueName in runKey.GetValueNames())
            {
                ct.ThrowIfCancellationRequested();
                var value = runKey.GetValue(valueName) as string;
                if (value is null) continue;

                if (value.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                    value.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(app.InstallLocation) &&
                     value.Contains(app.InstallLocation, StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add(new LeftoverCandidate
                    {
                        Id = Guid.NewGuid().ToString("N")[..16],
                        ApplicationId = app.Id,
                        DisplayName = $"Startup: {valueName}",
                        Type = LeftoverType.StartupEntry,
                        RegistryHive = RegistryHiveType.CurrentUser,
                        RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
                        RegistryValueName = valueName,
                        ConfidenceScore = 85,
                        ConfidenceLevel = ConfidenceLevel.High,
                        RiskLevel = RiskLevel.Low,
                        Evidence = [$"Startup entry references: {value}"],
                        IsSelectedByDefault = true,
                        SourceScanner = "Startup"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning startup entries");
        }
    }

    private static bool HasWriteAccess(string path)
    {
        try
        {
            var testFile = Path.Combine(path, $".uninstra_test_{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            return true;
        }
        catch { return false; }
    }
}
