namespace Uninstra.Application.Services;

using Microsoft.Extensions.Logging;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Results;

public sealed class BatchUninstallItem
{
    public required InstalledApplication Application { get; init; }
    public QueueItemStatus Status { get; set; } = QueueItemStatus.Waiting;
    public UninstallStatus? Result { get; set; }
    public IReadOnlyList<LeftoverCandidate> Leftovers { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class BatchUninstallCoordinator
{
    private readonly IUninstallService _uninstallService;
    private readonly ILeftoverScanner _leftoverScanner;
    private readonly ILogger<BatchUninstallCoordinator> _logger;

    public BatchUninstallCoordinator(
        IUninstallService uninstallService,
        ILeftoverScanner leftoverScanner,
        ILogger<BatchUninstallCoordinator> logger)
    {
        _uninstallService = uninstallService;
        _leftoverScanner = leftoverScanner;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        IList<BatchUninstallItem> queue,
        IProgress<(int Current, int Total, string Status)>? progress = null,
        CancellationToken ct = default)
    {
        for (int i = 0; i < queue.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = queue[i];

            if (item.Status == QueueItemStatus.Skipped || item.Status == QueueItemStatus.Cancelled)
                continue;

            item.Status = QueueItemStatus.Preparing;
            progress?.Report((i + 1, queue.Count, $"Preparing: {item.Application.DisplayName}"));

            item.Status = QueueItemStatus.RunningUninstaller;
            progress?.Report((i + 1, queue.Count, $"Uninstalling: {item.Application.DisplayName}"));

            var result = await _uninstallService.UninstallAsync(item.Application, ct: ct);
            if (!result.IsSuccess)
            {
                item.Status = QueueItemStatus.Failed;
                item.Errors.Add(result.Error?.Message ?? "Unknown error");
                _logger.LogWarning("Failed to uninstall {App}: {Error}", item.Application.DisplayName, result.Error?.Message);
                continue;
            }

            item.Result = result.Value;

            item.Status = QueueItemStatus.ScanningLeftovers;
            progress?.Report((i + 1, queue.Count, $"Scanning leftovers: {item.Application.DisplayName}"));

            try
            {
                var leftovers = await _leftoverScanner.ScanAsync(item.Application, ct);
                item.Leftovers = leftovers;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Leftover scan failed for {App}", item.Application.DisplayName);
            }

            item.Status = QueueItemStatus.Completed;
            progress?.Report((i + 1, queue.Count, $"Completed: {item.Application.DisplayName}"));
        }
    }
}
