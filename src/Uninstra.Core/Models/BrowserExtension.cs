namespace Uninstra.Core.Models;

public sealed record BrowserExtension
{
    public required string Id { get; init; }
    public string Browser { get; init; } = string.Empty;
    public string Profile { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ExtensionId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string InstallSource { get; init; } = string.Empty;
    public string ExtensionFolder { get; init; } = string.Empty;
    public List<string> Permissions { get; init; } = [];
    public bool IsManagedByPolicy { get; init; }
    public bool IsDeveloperMode { get; init; }
    public bool IsUnpacked { get; init; }
    public bool IsEnabled { get; init; } = true;
    public List<string> RiskIndicators { get; init; } = [];
}
