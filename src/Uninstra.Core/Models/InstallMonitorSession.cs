namespace Uninstra.Core.Models;

public sealed record InstallMonitorSession
{
    public required string SessionId { get; init; }
    public string InstallerPath { get; init; } = string.Empty;
    public string InstallerHash { get; init; } = string.Empty;
    public string InstallerPublisher { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int RootProcessId { get; init; }
    public List<int> ChildProcesses { get; init; } = [];
    public List<string> CreatedFiles { get; init; } = [];
    public List<string> ModifiedFiles { get; init; } = [];
    public List<string> CreatedDirectories { get; init; } = [];
    public List<string> RegistryChanges { get; init; } = [];
    public List<string> NewServices { get; init; } = [];
    public List<string> NewScheduledTasks { get; init; } = [];
    public List<string> NewStartupEntries { get; init; } = [];
    public List<string> DetectedApplications { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public string IncompleteMonitoringReason { get; init; } = string.Empty;
}
