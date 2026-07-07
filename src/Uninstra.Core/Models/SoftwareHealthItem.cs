namespace Uninstra.Core.Models;

public sealed record SoftwareHealthItem
{
    public required string Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = "No action required";
    public int AffectedCount { get; init; }
    public string NavigationTarget { get; init; } = string.Empty;
}
