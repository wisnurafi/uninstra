namespace Uninstra.Application.Services;

using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed class ReportCoordinator
{
    private readonly IReportService _reportService;
    private readonly IHistoryRepository _historyRepo;

    public ReportCoordinator(IReportService reportService, IHistoryRepository historyRepo)
    {
        _reportService = reportService;
        _historyRepo = historyRepo;
    }

    public async Task<(string JsonPath, string HtmlPath)> GenerateAsync(
        HistoryRecord record,
        IReadOnlyList<LeftoverCandidate>? leftovers = null,
        CancellationToken ct = default)
    {
        var jsonPath = await _reportService.GenerateJsonReportAsync(record, leftovers, ct);
        var htmlPath = await _reportService.GenerateHtmlReportAsync(record, leftovers, ct);
        return (jsonPath, htmlPath);
    }
}
