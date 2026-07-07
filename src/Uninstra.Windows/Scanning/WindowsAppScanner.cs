namespace Uninstra.Windows.Scanning;

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed class WindowsAppScanner : IWindowsAppScanner
{
    private readonly ILogger<WindowsAppScanner> _logger;

    private static readonly HashSet<string> ProtectedPackageFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.WindowsStore",
        "Microsoft.Windows.ShellExperienceHost",
        "Microsoft.Windows.StartMenuExperienceHost",
        "Microsoft.WindowsSecurity",
        "Microsoft.DesktopAppInstaller",
        "Microsoft.SecHealthUI",
        "Microsoft.WindowsTerminal",
        "Microsoft.WindowsCalculator",
        "Microsoft.WindowsNotepad"
    };

    public WindowsAppScanner(ILogger<WindowsAppScanner> logger) => _logger = logger;

    public Task<IReadOnlyList<WindowsApp>> ScanAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var apps = new List<WindowsApp>();

            try
            {
                // Use PowerShell to enumerate AppX packages (safest cross-version approach)
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"Get-AppxPackage | Select-Object Name,PackageFamilyName,Publisher,Version,InstallLocation,IsFramework,IsResourcePackage,SignatureKind | ConvertTo-Json -Depth 1\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc is null) return (IReadOnlyList<WindowsApp>)apps;

                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(30000);

                if (string.IsNullOrWhiteSpace(output)) return (IReadOnlyList<WindowsApp>)apps;

                using var doc = System.Text.Json.JsonDocument.Parse(output);
                var root = doc.RootElement;

                var elements = root.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? root.EnumerateArray().ToList()
                    : [root];

                foreach (var el in elements)
                {
                    ct.ThrowIfCancellationRequested();

                    var name = GetStr(el, "Name") ?? "";
                    var familyName = GetStr(el, "PackageFamilyName") ?? "";
                    var publisher = GetStr(el, "Publisher") ?? "";
                    var version = GetStr(el, "Version") ?? "";
                    var installLoc = GetStr(el, "InstallLocation") ?? "";
                    var isFramework = GetBool(el, "IsFramework");
                    var isResource = GetBool(el, "IsResourcePackage");

                    if (isResource) continue;

                    var isProtected = ProtectedPackageFamilies.Any(p => familyName.Contains(p, StringComparison.OrdinalIgnoreCase))
                        || isFramework;
                    var protectionReason = isFramework ? "Framework package" :
                        isProtected ? "System component" : "";

                    long size = 0;
                    try
                    {
                        if (!string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc))
                        {
                            size = new DirectoryInfo(installLoc)
                                .EnumerateFiles("*", SearchOption.AllDirectories)
                                .Sum(f => f.Length);
                        }
                    }
                    catch { /* access denied */ }

                    apps.Add(new WindowsApp
                    {
                        Id = familyName,
                        DisplayName = name,
                        PackageFamilyName = familyName,
                        Publisher = publisher,
                        Version = version,
                        InstallSize = size,
                        InstallLocation = installLoc,
                        IsFramework = isFramework,
                        IsProtected = isProtected,
                        ProtectionReason = protectionReason,
                        UserScope = "CurrentUser"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning Windows Apps");
            }

            return (IReadOnlyList<WindowsApp>)apps.OrderBy(a => a.DisplayName).ToList();
        }, ct);
    }

    private static string? GetStr(System.Text.Json.JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(System.Text.Json.JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.True;
}
