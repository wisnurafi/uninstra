namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;
using Uninstra.Core.Results;

public interface ILeftoverCleanupService
{
    Task<OperationResult<CleanupSummary>> CleanAsync(
        IReadOnlyList<LeftoverCandidate> items,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken ct = default);
}

public record CleanupSummary(int TotalItems, int Cleaned, int Failed, int Skipped, long FreedBytes);

public record CleanupProgress(string CurrentItem, int Current, int Total, string Status);
