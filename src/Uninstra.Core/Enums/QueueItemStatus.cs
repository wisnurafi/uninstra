namespace Uninstra.Core.Enums;

public enum QueueItemStatus
{
    Waiting,
    Preparing,
    RunningUninstaller,
    ScanningLeftovers,
    AwaitingReview,
    Cleaning,
    Completed,
    Skipped,
    Failed,
    Cancelled
}
