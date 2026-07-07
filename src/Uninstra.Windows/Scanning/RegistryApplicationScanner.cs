namespace Uninstra.Windows.Scanning;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Validation;

public sealed class RegistryApplicationScanner : IApplicationScanner
{
    private readonly ILogger<RegistryApplicationScanner> _logger;

    private static readonly (RegistryHive Hive, RegistryView View, string Path, RegistryHiveType HiveType, RegistryViewType ViewType)[] RegistryPaths =
    [
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", RegistryHiveType.LocalMachine, RegistryViewType.Registry64),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", RegistryHiveType.LocalMachine, RegistryViewType.Registry32),
        (RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", RegistryHiveType.CurrentUser, RegistryViewType.Registry64),
    ];

    public RegistryApplicationScanner(ILogger<RegistryApplicationScanner> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<InstalledApplication>> ScanAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var apps = new Dictionary<string, InstalledApplication>();

            foreach (var (hive, view, path, hiveType, viewType) in RegistryPaths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var uninstallKey = baseKey.OpenSubKey(path);
                    if (uninstallKey is null) continue;

                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            using var subKey = uninstallKey.OpenSubKey(subKeyName);
                            if (subKey is null) continue;

                            var app = ReadApplication(subKey, subKeyName, hiveType, viewType, $@"{path}\{subKeyName}");
                            if (app is null) continue;

                            // Deduplicate by stable ID
                            if (!apps.ContainsKey(app.Id))
                                apps[app.Id] = app;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Error reading registry key {Key}", subKeyName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning registry path {Path}", path);
                }
            }

            return (IReadOnlyList<InstalledApplication>)[.. apps.Values.OrderBy(a => a.DisplayName)];
        }, ct);
    }

    private InstalledApplication? ReadApplication(RegistryKey key, string keyName,
        RegistryHiveType hiveType, RegistryViewType viewType, string keyPath)
    {
        var displayName = key.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(displayName)) return null;

        // Skip entries marked as system component unless they have a display name
        var systemComponent = GetDwordValue(key, "SystemComponent") == 1;
        var noRemove = GetDwordValue(key, "NoRemove") == 1;

        var uninstallString = key.GetValue("UninstallString") as string ?? "";
        var quietUninstallString = key.GetValue("QuietUninstallString") as string ?? "";

        // Skip if both are missing and no remove
        if (string.IsNullOrWhiteSpace(uninstallString) && noRemove && systemComponent)
            return null;

        var publisher = key.GetValue("Publisher") as string ?? "";
        var displayVersion = key.GetValue("DisplayVersion") as string ?? "";
        var installLocation = key.GetValue("InstallLocation") as string ?? "";
        var installDate = ParseInstallDate(key.GetValue("InstallDate") as string);
        var displayIcon = key.GetValue("DisplayIcon") as string ?? "";
        var estimatedSize = GetDwordValue(key, "EstimatedSize") * 1024L; // KB to bytes
        var productCode = key.GetValue("ProductCode") as string ?? keyName;
        var modifyPath = key.GetValue("ModifyPath") as string ?? "";
        var windowsInstaller = GetDwordValue(key, "WindowsInstaller") == 1;
        var releaseType = key.GetValue("ReleaseType") as string ?? "";
        var parentKeyName = key.GetValue("ParentKeyName") as string ?? "";
        var urlInfoAbout = key.GetValue("URLInfoAbout") as string ?? "";
        var helpLink = key.GetValue("HelpLink") as string ?? "";
        var comments = key.GetValue("Comments") as string ?? "";

        var isUpdate = !string.IsNullOrEmpty(releaseType) &&
            (releaseType.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
             releaseType.Contains("Hotfix", StringComparison.OrdinalIgnoreCase) ||
             !string.IsNullOrEmpty(parentKeyName));

        var installerType = DetectInstallerType(uninstallString, quietUninstallString, windowsInstaller, productCode);
        var architecture = viewType == RegistryViewType.Registry32 ? AppArchitecture.X86 : AppArchitecture.X64;
        var isRuntime = DetectRuntime(displayName, publisher);
        var isDriverRelated = DetectDriver(displayName, publisher);
        var category = ClassifyApplication(displayName, publisher, systemComponent, isRuntime, isUpdate, isDriverRelated);
        var normalizedName = NameNormalizer.Normalize(displayName);

        var (isProtected, protectionReason) = Core.Safety.ProtectedLists.Evaluate(displayName, publisher, systemComponent, isRuntime);

        var evidence = new List<string>();
        evidence.Add($"Registry: {hiveType}/{viewType}/{keyName}");
        if (!string.IsNullOrEmpty(installLocation)) evidence.Add($"InstallLocation: {installLocation}");
        if (windowsInstaller) evidence.Add("Windows Installer (MSI)");

        var stableId = GenerateStableId(productCode, keyName, uninstallString, installLocation, normalizedName, publisher);

        return new InstalledApplication
        {
            Id = stableId,
            DisplayName = displayName,
            NormalizedName = normalizedName,
            DisplayVersion = displayVersion,
            Publisher = publisher,
            InstallDate = installDate,
            InstallLocation = installLocation,
            UninstallCommand = uninstallString,
            QuietUninstallCommand = quietUninstallString,
            ModifyCommand = modifyPath,
            DisplayIconPath = displayIcon,
            EstimatedSizeBytes = estimatedSize,
            ProductCode = productCode,
            InstallerType = installerType,
            Architecture = architecture,
            ApplicationCategory = category,
            RegistryHive = hiveType,
            RegistryKeyPath = keyPath,
            RegistryView = viewType,
            IsSystemComponent = systemComponent,
            IsRuntime = isRuntime,
            IsUpdate = isUpdate,
            IsDriverRelated = isDriverRelated,
            IsStoreApplication = false,
            IsProtected = isProtected,
            ProtectionReason = protectionReason,
            DetectionEvidence = evidence,
            UrlInfoAbout = urlInfoAbout,
            HelpLink = helpLink,
            Comments = comments
        };
    }

    private static string GenerateStableId(string productCode, string keyName,
        string uninstallString, string installLocation, string normalizedName, string publisher)
    {
        // Priority: ProductCode > KeyName combo
        var input = !string.IsNullOrEmpty(productCode) && productCode != keyName
            ? productCode
            : $"{keyName}|{normalizedName}|{publisher}";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }

    private static InstallerType DetectInstallerType(string uninstall, string quietUninstall,
        bool windowsInstaller, string productCode)
    {
        if (windowsInstaller || uninstall.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
            return InstallerType.Msi;
        if (uninstall.Contains("unins000", StringComparison.OrdinalIgnoreCase) ||
            uninstall.Contains("unins001", StringComparison.OrdinalIgnoreCase))
            return InstallerType.InnoSetup;
        if (quietUninstall.Contains("/S", StringComparison.Ordinal) &&
            !quietUninstall.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
            return InstallerType.Nsis;
        if (uninstall.Contains("InstallShield", StringComparison.OrdinalIgnoreCase))
            return InstallerType.InstallShield;
        if (uninstall.Contains("Update.exe", StringComparison.OrdinalIgnoreCase) &&
            uninstall.Contains("--uninstall", StringComparison.OrdinalIgnoreCase))
            return InstallerType.Squirrel;
        if (string.IsNullOrWhiteSpace(uninstall))
            return InstallerType.MissingUninstaller;

        return InstallerType.CustomExe;
    }

    private static bool DetectRuntime(string name, string publisher)
    {
        var runtimePatterns = new[] { "Runtime", ".NET", "Visual C++", "Redistributable", "SDK", "Framework" };
        return runtimePatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool DetectDriver(string name, string publisher)
    {
        return name.Contains("Driver", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Chipset", StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationCategory ClassifyApplication(string name, string publisher,
        bool systemComponent, bool isRuntime, bool isUpdate, bool isDriverRelated)
    {
        if (systemComponent) return ApplicationCategory.SystemComponent;
        if (isRuntime) return ApplicationCategory.Runtime;
        if (isUpdate) return ApplicationCategory.Update;
        if (isDriverRelated) return ApplicationCategory.DriverRelated;
        return ApplicationCategory.UserApplication;
    }

    private static DateTime? ParseInstallDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParseExact(value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            return date;
        return null;
    }

    private static int GetDwordValue(RegistryKey key, string name)
    {
        var val = key.GetValue(name);
        return val is int i ? i : 0;
    }
}
