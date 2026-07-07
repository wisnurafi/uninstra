namespace Uninstra.Core.Models;

using Uninstra.Core.Enums;

public sealed record JunkCategory
{
    public required string Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public long DetectedSize { get; init; }
    public RiskLevel Risk { get; init; }
    public bool RequiresElevation { get; init; }
    public List<JunkItem> Items { get; init; } = [];
}

public sealed record JunkItem
{
    public required string Id { get; init; }
    public string Path { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime? LastModified { get; init; }
    public bool IsLocked { get; init; }
    public string CategoryId { get; init; } = string.Empty;
}
