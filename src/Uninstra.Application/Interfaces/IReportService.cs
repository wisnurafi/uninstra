namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;

public interface IReportService
{
    Task<string> GenerateJsonReportAsync(HistoryRecord record, IReadOnlyList<LeftoverCandidate>? leftovers = null, CancellationToken ct = default);
    Task<string> GenerateHtmlReportAsync(HistoryRecord record, IReadOnlyList<LeftoverCandidate>? leftovers = null, CancellationToken ct = default);
}
