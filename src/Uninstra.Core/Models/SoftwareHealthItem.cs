namespace Uninstra.Core.Models;

using System.Collections.ObjectModel;

public sealed record SoftwareHealthItem
{
    public required string Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = "No action required";
    public int AffectedCount { get; init; }
    public string NavigationTarget { get; init; } = string.Empty;
    public ObservableCollection<HealthIssueDetail> Details { get; init; } = [];
    public bool HasDetails => Details.Count > 0;
}

/// <summary>
/// Detail item untuk setiap issue di Software Health
/// </summary>
public sealed record HealthIssueDetail
{
    public required string Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string InstallLocation { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime? InstallDate { get; init; }
    public string UninstallCommand { get; init; } = string.Empty;
    public string RegistryKeyPath { get; init; } = string.Empty;
    public bool CanFix { get; init; }
    public string FixAction { get; init; } = string.Empty;
}
