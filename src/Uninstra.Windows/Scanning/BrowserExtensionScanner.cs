namespace Uninstra.Windows.Scanning;

using Microsoft.Extensions.Logging;
using System.Text.Json;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed class BrowserExtensionScanner : IBrowserExtensionScanner
{
    private readonly ILogger<BrowserExtensionScanner> _logger;

    public BrowserExtensionScanner(ILogger<BrowserExtensionScanner> logger) => _logger = logger;

    public Task<IReadOnlyList<BrowserExtension>> ScanAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var extensions = new List<BrowserExtension>();

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Chrome
            ScanChromium(extensions, "Google Chrome",
                Path.Combine(localAppData, @"Google\Chrome\User Data"), ct);

            // Edge
            ScanChromium(extensions, "Microsoft Edge",
                Path.Combine(localAppData, @"Microsoft\Edge\User Data"), ct);

            // Firefox
            ScanFirefox(extensions,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Mozilla\Firefox\Profiles"), ct);

            return (IReadOnlyList<BrowserExtension>)extensions;
        }, ct);
    }

    private void ScanChromium(List<BrowserExtension> results, string browserName,
        string userDataPath, CancellationToken ct)
    {
        if (!Directory.Exists(userDataPath)) return;

        try
        {
            // Find all profiles
            var profiles = new List<string>();
            var defaultProfile = Path.Combine(userDataPath, "Default");
            if (Directory.Exists(defaultProfile)) profiles.Add(defaultProfile);

            foreach (var dir in Directory.GetDirectories(userDataPath, "Profile *"))
                profiles.Add(dir);

            foreach (var profile in profiles)
            {
                ct.ThrowIfCancellationRequested();
                var extDir = Path.Combine(profile, "Extensions");
                if (!Directory.Exists(extDir)) continue;

                var profileName = Path.GetFileName(profile);
                var prefsFile = Path.Combine(profile, "Preferences");
                var prefs = ReadJsonFile(prefsFile);

                foreach (var extFolder in Directory.GetDirectories(extDir))
                {
                    ct.ThrowIfCancellationRequested();
                    var extId = Path.GetFileName(extFolder);

                    // Find the latest version folder
                    var versionDirs = Directory.GetDirectories(extFolder);
                    if (versionDirs.Length == 0) continue;

                    var latestVersion = versionDirs.OrderByDescending(d => d).First();
                    var manifestFile = Path.Combine(latestVersion, "manifest.json");

                    if (!File.Exists(manifestFile)) continue;

                    try
                    {
                        var manifest = ReadJsonFile(manifestFile);
                        if (manifest is null) continue;

                        var name = GetJsonString(manifest, "name") ?? extId;
                        var version = GetJsonString(manifest, "version") ?? "Unknown";
                        var description = GetJsonString(manifest, "description") ?? "";
                        var permissions = GetJsonStringArray(manifest, "permissions");

                        var risks = new List<string>();
                        if (permissions.Count > 10) risks.Add("High permission count");
                        if (permissions.Any(p => p is "tabs" or "webRequest" or "webRequestBlocking" or "<all_urls>"))
                            risks.Add("Sensitive permissions detected");

                        results.Add(new BrowserExtension
                        {
                            Id = $"{browserName}_{profileName}_{extId}",
                            Browser = browserName,
                            Profile = profileName,
                            Name = name.StartsWith("__MSG_") ? extId : name,
                            ExtensionId = extId,
                            Version = version,
                            Description = description.StartsWith("__MSG_") ? "" : description,
                            ExtensionFolder = latestVersion,
                            Permissions = permissions,
                            RiskIndicators = risks
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error reading extension manifest: {Path}", manifestFile);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning {Browser}", browserName);
        }
    }

    private void ScanFirefox(List<BrowserExtension> results, string profilesPath, CancellationToken ct)
    {
        if (!Directory.Exists(profilesPath)) return;

        try
        {
            foreach (var profileDir in Directory.GetDirectories(profilesPath))
            {
                ct.ThrowIfCancellationRequested();
                var extensionsJson = Path.Combine(profileDir, "extensions.json");
                if (!File.Exists(extensionsJson)) continue;

                var profileName = Path.GetFileName(profileDir);

                try
                {
                    var json = ReadJsonFile(extensionsJson);
                    if (json is null) continue;

                    if (json.Value.TryGetProperty("addons", out var addons) && addons.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var addon in addons.EnumerateArray())
                        {
                            ct.ThrowIfCancellationRequested();
                            var id = GetJsonString(addon, "id") ?? "";
                            var name = GetJsonString(addon, "name") ?? id;
                            var version = GetJsonString(addon, "version") ?? "";
                            var description = GetJsonString(addon, "description") ?? "";
                            var isActive = addon.TryGetProperty("active", out var active) && active.GetBoolean();

                            var risks = new List<string>();
                            if (addon.TryGetProperty("userDisabled", out var disabled) && disabled.GetBoolean())
                                risks.Add("User disabled");

                            results.Add(new BrowserExtension
                            {
                                Id = $"Firefox_{profileName}_{id}",
                                Browser = "Mozilla Firefox",
                                Profile = profileName,
                                Name = name,
                                ExtensionId = id,
                                Version = version,
                                Description = description,
                                IsEnabled = isActive,
                                RiskIndicators = risks
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error reading Firefox extensions: {Path}", extensionsJson);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning Firefox profiles");
        }
    }

    private JsonElement? ReadJsonFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var text = File.ReadAllText(path);
            return JsonDocument.Parse(text).RootElement;
        }
        catch { return null; }
    }

    private static string? GetJsonString(JsonElement? element, string property)
    {
        if (element is null) return null;
        if (element.Value.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }

    private static string? GetJsonString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }

    private static List<string> GetJsonStringArray(JsonElement? element, string property)
    {
        if (element is null) return [];
        if (!element.Value.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
    }
}
