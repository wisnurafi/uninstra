namespace Uninstra.Core.Models;

using Uninstra.Core.Enums;

public sealed record QuarantineManifest
{
    public required string OperationId { get; init; }
    public required string ApplicationId { get; init; }
    public string ApplicationName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string OriginalPath { get; init; } = string.Empty;
    public string QuarantinePath { get; init; } = string.Empty;
    public long Size { get; init; }
    public string Hash { get; init; } = string.Empty;
    public LeftoverType ItemType { get; init; }
    public string? RegistryBackup { get; init; }
    public bool CanRestore { get; init; } = true;
    public List<string> RestoreWarnings { get; init; } = [];
}
