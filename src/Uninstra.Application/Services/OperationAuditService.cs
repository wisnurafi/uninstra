namespace Uninstra.Application.Services;

using Microsoft.Extensions.Logging;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Results;

/// <summary>
/// Central audit trail writer. Every uninstall/cleanup operation flows through
/// here so the History page reflects reality instead of staying empty.
/// </summary>
public sealed class OperationAuditService
{
    private readonly IHistoryRepository _historyRepo;
    private readonly ILogger<OperationAuditService> _logger;

    public OperationAuditService(IHistoryRepository historyRepo, ILogger<OperationAuditService> logger)
    {
        _historyRepo = historyRepo;
        _logger = logger;
    }

    public async Task<HistoryRecord> StartAsync(
        OperationType type, InstalledApplication app, CancellationToken ct = default)
    {
        var record = new HistoryRecord
        {
            OperationId = Guid.NewGuid().ToString("N")[..16],
            OperationType = type,
            ApplicationId = app.Id,
            ApplicationName = app.DisplayName,
            Publisher = app.Publisher,
            StartedAt = DateTime.UtcNow,
            Status = UninstallStatus.UnknownResult
        };

        try
        {
            await _historyRepo.AddAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write history start record {Op}", record.OperationId);
        }

        return record;
    }

    public async Task CompleteAsync(
        HistoryRecord record,
        UninstallStatus status,
        int? exitCode = null,
        int itemsDetected = 0,
        int itemsCleaned = 0,
        int itemsSkipped = 0,
        long recoveredBytes = 0,
        string restorePointStatus = "",
        bool quarantineAvailable = false,
        int warningCount = 0,
        int errorCount = 0,
        string reportPath = "",
        CancellationToken ct = default)
    {
        var completed = record with
        {
            Status = status,
            ExitCode = exitCode,
            CompletedAt = DateTime.UtcNow,
            ItemsDetected = itemsDetected,
            ItemsCleaned = itemsCleaned,
            ItemsSkipped = itemsSkipped,
            RecoveredBytes = recoveredBytes,
            RestorePointStatus = restorePointStatus,
            QuarantineAvailable = quarantineAvailable,
            WarningCount = warningCount,
            ErrorCount = errorCount,
            ReportPath = reportPath
        };

        try
        {
            await _historyRepo.DeleteAsync(record.OperationId, ct).ConfigureAwait(false);
            await _historyRepo.AddAsync(completed, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update history record {Op}", record.OperationId);
        }
    }
}
