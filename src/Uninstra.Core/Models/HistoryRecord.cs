namespace Uninstra.Core.Models;

using Uninstra.Core.Enums;

public sealed record HistoryRecord
{
    public required string OperationId { get; init; }
    public OperationType OperationType { get; init; }
    public string ApplicationId { get; init; } = string.Empty;
    public string ApplicationName { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public UninstallStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public int ItemsDetected { get; init; }
    public int ItemsCleaned { get; init; }
    public int ItemsSkipped { get; init; }
    public long RecoveredBytes { get; init; }
    public string RestorePointStatus { get; init; } = string.Empty;
    public bool QuarantineAvailable { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public string ReportPath { get; init; } = string.Empty;
}
