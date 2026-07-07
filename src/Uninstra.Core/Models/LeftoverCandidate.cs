namespace Uninstra.Core.Models;

using Uninstra.Core.Enums;

public sealed record LeftoverCandidate
{
    public required string Id { get; init; }
    public required string ApplicationId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public LeftoverType Type { get; init; }
    public string Path { get; init; } = string.Empty;
    public RegistryHiveType? RegistryHive { get; init; }
    public string RegistryPath { get; init; } = string.Empty;
    public string RegistryValueName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public int ConfidenceScore { get; init; }
    public ConfidenceLevel ConfidenceLevel { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public List<string> Evidence { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public bool IsSelectedByDefault { get; init; }
    public bool RequiresElevation { get; init; }
    public bool IsProtected { get; init; }
    public string ProtectionReason { get; init; } = string.Empty;
    public bool CanRollback { get; init; } = true;
    public DateTime? LastModified { get; init; }
    public string SourceScanner { get; init; } = string.Empty;
}
