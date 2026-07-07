namespace Uninstra.Core.Models;

public sealed record WindowsApp
{
    public required string Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string PackageFamilyName { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long InstallSize { get; init; }
    public string InstallLocation { get; init; } = string.Empty;
    public bool IsFramework { get; init; }
    public bool IsDependency { get; init; }
    public bool IsProtected { get; init; }
    public string ProtectionReason { get; init; } = string.Empty;
    public string UserScope { get; init; } = string.Empty;
    public byte[]? Logo { get; init; }
}
