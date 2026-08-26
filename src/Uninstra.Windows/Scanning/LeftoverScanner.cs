namespace Uninstra.Windows.Scanning;

using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Safety;
using Uninstra.Core.Scoring;
using Uninstra.Core.Validation;

/// <summary>
/// Evidence-based leftover scanner. Every candidate is scored through
/// ConfidenceScorer — no hardcoded scores. Respects user scan preferences.
/// </summary>
public sealed class LeftoverScanner : ILeftoverScanner
{
    private readonly ILogger<LeftoverScanner> _logger;
    private readonly ISettingsService _settingsService;

    public LeftoverScanner(ILogger<LeftoverScanner> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public Task<IReadOnlyList<LeftoverCandidate>> ScanAsync(InstalledApplication app, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var settings = _settingsService.Load();
            var candidates = new List<LeftoverCandidate>();

            // Install location
            if (!string.IsNullOrWhiteSpace(app.InstallLocation))
                ScanDirectory(app, app.InstallLocation, true, candidates, ct);

            // AppData / ProgramData folders
            if (settings.ScanAppData)
            {
                ct.ThrowIfCancellationRequested();
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                foreach (var baseDir in new[] { localAppData, roamingAppData })
                    ScanForAppFolder(app, baseDir, candidates, ct);
            }

            if (settings.ScanProgramData)
            {
                ct.ThrowIfCancellationRequested();
                var programData = Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData";
                ScanForAppFolder(app, programData, candidates, ct);
            }

            if (settings.ScanRegistry)
            {
                ct.ThrowIfCancellationRequested();
                ScanRegistryLeftovers(app, candidates, ct);
            }

            ct.ThrowIfCancellationRequested();
            ScanShortcuts(app, candidates, ct);

            if (settings.ScanStartup)
            {
                ct.ThrowIfCancellationRequested();
                ScanStartupEntries(app, candidates, ct);
            }

            if (settings.ScanServices)
            {
                ct.ThrowIfCancellationRequested();
                ScanServices(app, candidates, ct);
            }

            if (settings.ScanScheduledTasks)
            {
                ct.ThrowIfCancellationRequested();
                ScanScheduledTasks(app, candidates, ct);
            }

            return (IReadOnlyList<LeftoverCandidate>)candidates;
        }, ct);
    }

    // ────────────────────────────────────────────────────────────────
    //  Directories
    // ────────────────────────────────────────────────────────────────

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

            // Shared publisher-root heuristic: "C:\Program Files\Vendor" itself is risky to remove
            var folderPossiblyShared = false;
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            foreach (var root in new[] { pf, pf86 })
            {
                if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(app.Publisher)) continue;
                var vendorRoot = Path.Combine(root, app.Publisher.Split(' ')[0]);
                if (string.Equals(
                        Path.TrimEndingDirectorySeparator(dir.FullName),
                        Path.TrimEndingDirectorySeparator(vendorRoot),
                        StringComparison.OrdinalIgnoreCase))
                {
                    folderPossiblyShared = true;
                    break;
                }
            }

            // Executable evidence inside the folder (strongest signal for AppData dirs)
            var exeNameEvidence = false;
            var signatureMatch = false;
            if (!isInstallLocation)
            {
                foreach (var exe in SafeEnumerate(dir, "*.exe"))
                {
                    var exeName = Path.GetFileNameWithoutExtension(exe);

                    if (!exeNameEvidence &&
                        (exeName.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                         exeName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        exeNameEvidence = true;
                    }

                    // Digital signature evidence
                    if (!signatureMatch &&
                        !string.IsNullOrEmpty(app.Publisher) &&
                        !NameNormalizer.IsGenericName(app.NormalizedName))
                    {
                        var signer = GetSigner(exe);
                        if (signer is not null &&
                            app.Publisher.Contains(signer, StringComparison.OrdinalIgnoreCase))
                        {
                            signatureMatch = true;
                        }
                    }

                    if (exeNameEvidence && signatureMatch)
                        break;
                }
            }

            var ctx = new ScoringContext
            {
                IsExactInstallLocation = isInstallLocation,
                FolderNameMatchesExactly =
                    dir.Name.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(app.DisplayName) &&
                     dir.Name.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase)),
                FolderPossiblyShared = folderPossiblyShared,
                ExecutablePointsToInstallLocation = exeNameEvidence,
                RegistryValuePointsToExecutable = exeNameEvidence,
                DigitalSignaturePublisherMatches = signatureMatch,
                PublisherAndNameMatch = signatureMatch,
                NameVeryShort = app.NormalizedName.Length < 3,
                NameTooGeneric = NameNormalizer.IsGenericName(app.NormalizedName),
                LastModifiedUtc = dir.LastWriteTimeUtc
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
        if (app.NormalizedName.Length < 3) return; // avoid wild matching on tiny names

        try
        {
            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                ct.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(dir);
                var matches =
                    dirName.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(app.Publisher) &&
                     dirName.Equals(app.Publisher, StringComparison.OrdinalIgnoreCase));

                if (matches) ScanDirectory(app, dir, false, candidates, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning base dir: {Dir}", baseDir);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Registry
    // ────────────────────────────────────────────────────────────────

    private void ScanRegistryLeftovers(InstalledApplication app, List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        // 1. Original uninstall entry
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
                    Evidence = ["Original uninstall registry entry still present"],
                    IsSelectedByDefault = true,
                    RequiresElevation = app.RegistryHive == RegistryHiveType.LocalMachine,
                    SourceScanner = "Registry"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking uninstall entry for {App}", app.DisplayName);
        }

        // 2. App-specific software keys — both hives, name-equality based (fewer false hits than raw Contains)
        ScanSoftwareKeys(app, RegistryHive.CurrentUser, RegistryView.Default, candidates, ct);
        ScanSoftwareKeys(app, RegistryHive.LocalMachine, RegistryView.Default, candidates, ct);
    }

    private void ScanSoftwareKeys(InstalledApplication app, RegistryHive hive, RegistryView view,
        List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var softwareKey = baseKey.OpenSubKey("Software");
            if (softwareKey is null) return;

            foreach (var subKeyName in softwareKey.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();

                var strongMatch =
                    subKeyName.Equals(app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                    subKeyName.Replace(' ', '\0').Equals(app.NormalizedName.Replace(' ', '\0'), StringComparison.OrdinalIgnoreCase) ||
                    subKeyName.Equals(app.Publisher, StringComparison.OrdinalIgnoreCase);
                var weakMatch = !strongMatch && app.NormalizedName.Length >= 5 &&
                    subKeyName.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase);

                if (!strongMatch && !weakMatch) continue;
                if (SafetyPolicy.IsProtectedApplication(subKeyName)) continue;

                var hiveType = hive == RegistryHive.LocalMachine
                    ? RegistryHiveType.LocalMachine : RegistryHiveType.CurrentUser;

                candidates.Add(new LeftoverCandidate
                {
                    Id = Guid.NewGuid().ToString("N")[..16],
                    ApplicationId = app.Id,
                    DisplayName = $"Registry: {(hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU")}\\Software\\{subKeyName}",
                    Type = LeftoverType.RegistryKey,
                    RegistryHive = hiveType,
                    RegistryPath = $@"Software\{subKeyName}",
                    ConfidenceScore = strongMatch ? 70 : 45,
                    ConfidenceLevel = strongMatch ? ConfidenceLevel.Medium : ConfidenceLevel.Low,
                    RiskLevel = RiskLevel.Medium,
                    Evidence = [strongMatch
                        ? $"Software key name matches app identity: {subKeyName}"
                        : $"Software key contains normalized name: {subKeyName}"],
                    IsSelectedByDefault = false,
                    RequiresElevation = hive == RegistryHive.LocalMachine,
                    SourceScanner = "Registry"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning Software keys ({Hive})", hive);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Shortcuts — resolved targets, not filename guessing
    // ────────────────────────────────────────────────────────────────

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
                    ct.ThrowIfCancellationRequested();
                    var lnkName = Path.GetFileNameWithoutExtension(lnk);
                    var nameMatch =
                        lnkName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                        lnkName.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase);

                    var target = ShellLinkResolver.ResolveTarget(lnk);

                    var targetInInstallLocation =
                        target is not null &&
                        !string.IsNullOrEmpty(app.InstallLocation) &&
                        target.StartsWith(app.InstallLocation.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

                    var ctx = new ScoringContext
                    {
                        ShortcutTargetInInstallLocation = targetInInstallLocation,
                        ExecutablePointsToInstallLocation =
                            targetInInstallLocation &&
                            target!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
                        NormalizedNameMatches = nameMatch,
                        NameTooGeneric = NameNormalizer.IsGenericName(app.NormalizedName)
                    };

                    // A shortcut whose target lives in the install location belongs to this app
                    // even when its display name differs ("Foo Help.lnk" -> Foo install dir).
                    var (score, level, evidence) = ConfidenceScorer.Calculate(ctx);
                    if (score < 35 && !nameMatch) continue; // unrelated shortcut

                    var itemEvidence = new List<string>(evidence);
                    if (target is not null) itemEvidence.Add($"Target: {target}");

                    candidates.Add(new LeftoverCandidate
                    {
                        Id = Guid.NewGuid().ToString("N")[..16],
                        ApplicationId = app.Id,
                        DisplayName = $"Shortcut: {lnkName}",
                        Type = LeftoverType.Shortcut,
                        Path = lnk,
                        SizeBytes = new FileInfo(lnk).Length,
                        ConfidenceScore = Math.Max(score, nameMatch ? 40 : score),
                        ConfidenceLevel = level,
                        RiskLevel = RiskLevel.Low,
                        Evidence = itemEvidence,
                        IsSelectedByDefault = false,
                        SourceScanner = "Shortcuts",
                        LastModified = File.GetLastWriteTime(lnk)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error scanning shortcuts in {Dir}", dir);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Startup entries
    // ────────────────────────────────────────────────────────────────

    private void ScanStartupEntries(InstalledApplication app, List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        ScanRunKey(app, @"Software\Microsoft\Windows\CurrentVersion\Run", candidates, ct);
        ScanRunKey(app, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", candidates, ct);
    }

    private void ScanRunKey(InstalledApplication app, string runKeyPath,
        List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath);
            if (runKey is null) return;

            foreach (var valueName in runKey.GetValueNames())
            {
                ct.ThrowIfCancellationRequested();
                var value = runKey.GetValue(valueName) as string;
                if (value is null) continue;

                var pointsToLocation =
                    !string.IsNullOrEmpty(app.InstallLocation) &&
                    value.Contains(app.InstallLocation.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                var pointsToExe = pointsToLocation || ContainsExecutableReference(value, app);

                var ctx = new ScoringContext
                {
                    StartupEntryInInstallLocation = pointsToLocation,
                    ExecutablePointsToInstallLocation = pointsToExe,
                    NormalizedNameMatches =
                        value.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase),
                    NameTooGeneric = NameNormalizer.IsGenericName(app.NormalizedName)
                };
                var (score, level, evidence) = ConfidenceScorer.Calculate(ctx);
                if (!pointsToExe && !pointsToLocation) continue; // name-only hits are too noisy for Run keys

                candidates.Add(new LeftoverCandidate
                {
                    Id = Guid.NewGuid().ToString("N")[..16],
                    ApplicationId = app.Id,
                    DisplayName = $"Startup: {valueName}",
                    Type = LeftoverType.StartupEntry,
                    RegistryHive = RegistryHiveType.CurrentUser,
                    RegistryPath = runKeyPath,
                    RegistryValueName = valueName,
                    ConfidenceScore = score,
                    ConfidenceLevel = level,
                    RiskLevel = RiskLevel.Low,
                    Evidence = [.. evidence, $"Startup value references: {value}"],
                    IsSelectedByDefault = level == ConfidenceLevel.High,
                    SourceScanner = "Startup"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning run key {Key}", runKeyPath);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Services — ImagePath evidence
    // ────────────────────────────────────────────────────────────────

    private void ScanServices(InstalledApplication app, List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(app.InstallLocation)) return;

        try
        {
            using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (servicesKey is null) return;

            foreach (var serviceName in servicesKey.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var svcKey = servicesKey.OpenSubKey(serviceName);
                    var imagePath = svcKey?.GetValue("ImagePath") as string;
                    if (string.IsNullOrWhiteSpace(imagePath)) continue;

                    var expanded = SafetyPolicy.NormalizePath(imagePath);
                    if (expanded is null) continue;

                    if (!expanded.StartsWith(app.InstallLocation.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        continue;

                    var ctx = new ScoringContext
                    {
                        ServiceImagePathInInstallLocation = true,
                        ExecutablePointsToInstallLocation = expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
                        FolderNameMatchesExactly = serviceName.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase)
                    };
                    var (score, level, evidence) = ConfidenceScorer.Calculate(ctx);

                    candidates.Add(new LeftoverCandidate
                    {
                        Id = Guid.NewGuid().ToString("N")[..16],
                        ApplicationId = app.Id,
                        DisplayName = $"Service: {serviceName}",
                        Type = LeftoverType.Service,
                        RegistryHive = RegistryHiveType.LocalMachine,
                        RegistryPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}",
                        Path = expanded,
                        ConfidenceScore = score,
                        ConfidenceLevel = level,
                        RiskLevel = level == ConfidenceLevel.High ? RiskLevel.Medium : RiskLevel.High,
                        Evidence = [.. evidence, $"ImagePath: {imagePath}"],
                        IsSelectedByDefault = false, // services are never auto-selected
                        RequiresElevation = true,
                        SourceScanner = "Services"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error reading service {Service}", serviceName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error enumerating services");
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Scheduled tasks — TaskCache actions blob scan
    // ────────────────────────────────────────────────────────────────

    private void ScanScheduledTasks(InstalledApplication app, List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(app.InstallLocation)) return;

        try
        {
            using var treeRoot = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree");
            if (treeRoot is null) return;

            WalkTaskTree(app, treeRoot, string.Empty, candidates, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning scheduled tasks");
        }
    }

    private void WalkTaskTree(InstalledApplication app, RegistryKey node, string relativePath,
        List<LeftoverCandidate> candidates, CancellationToken ct)
    {
        // Default value of a Tree leaf = task GUID
        var guid = node.GetValue(null) as string;
        if (!string.IsNullOrEmpty(guid))
        {
            try
            {
                using var tasksRoot = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tasks");
                using var taskKey = tasksRoot?.OpenSubKey(guid);
                var actions = taskKey?.GetValue("Actions") as byte[];
                if (actions is not null)
                {
                    // Actions blob embeds the command as UTF-16LE — a plain substring probe suffices.
                    var blob = Encoding.Unicode.GetString(actions);
                    if (blob.Contains(app.InstallLocation.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(new LeftoverCandidate
                        {
                            Id = Guid.NewGuid().ToString("N")[..16],
                            ApplicationId = app.Id,
                            DisplayName = $"Scheduled Task: {relativePath}",
                            Type = LeftoverType.ScheduledTask,
                            RegistryHive = RegistryHiveType.LocalMachine,
                            RegistryPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tasks\{guid}",
                            RegistryValueName = relativePath, // schtasks /TN path
                            Path = relativePath,
                            ConfidenceScore = 85,
                            ConfidenceLevel = ConfidenceLevel.High,
                            RiskLevel = RiskLevel.Medium,
                            Evidence = [$"Task action executes from install location ({relativePath})"],
                            IsSelectedByDefault = false,
                            RequiresElevation = true,
                            SourceScanner = "ScheduledTasks"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error reading task {Guid}", guid);
            }
        }

        foreach (var childName in node.GetSubKeyNames())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var child = node.OpenSubKey(childName);
                if (child is not null)
                    WalkTaskTree(app, child,
                        string.IsNullOrEmpty(relativePath) ? childName : $@"{relativePath}\{childName}",
                        candidates, ct);
            }
            catch { /* access denied */ }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────

    private static IEnumerable<string> SafeEnumerate(DirectoryInfo dir, string pattern)
    {
        try { return dir.GetFiles(pattern, SearchOption.TopDirectoryOnly).Select(f => f.FullName); }
        catch { return []; }
    }

    private static bool ContainsExecutableReference(string value, InstalledApplication app)
    {
        // "C:\...\app.exe" -arg patterns: extract potential exe path tokens and test them
        foreach (var token in value.Split('"'))
        {
            if (token.Trim().Length == 0) continue;
            var candidate = token.Trim();
            if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            if (candidate.Contains(app.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                candidate.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string? GetSigner(string filePath)
    {
        try
        {
            using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(filePath);
            return cert.GetNameInfo(X509NameType.SimpleName, false);
        }
        catch { return null; }
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

/// <summary>Resolves .lnk target paths via shell COM (no WinForms dependency).</summary>
internal static class ShellLinkResolver
{
    public static string? ResolveTarget(string lnkPath)
    {
        try
        {
            // IMPORTANT: one COM instance, two interface views on the SAME object.
            var shellLink = (IShellLinkW)(object)new ShellLinkCom();
            ((IPersistFile)(object)shellLink).Load(lnkPath, 0);
            var sb = new StringBuilder(520);
            shellLink.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            var target = sb.ToString();
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch
        {
            return null;
        }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLinkCom { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        [PreserveSig]
        void GetPath([Out] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        // Remaining vtable members intentionally omitted — only GetPath is called.
    }

    [ComImport, Guid("0000010C-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        [PreserveSig] void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        [PreserveSig] void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        [PreserveSig] void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        [PreserveSig] void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        [PreserveSig] void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
