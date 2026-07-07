namespace Uninstra.Infrastructure.Services;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed class ReportService : IReportService
{
    private readonly ILogger<ReportService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ReportService(ILogger<ReportService> logger) => _logger = logger;

    private static string GetReportDirectory()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dir = Path.Combine(docs, "Uninstra Reports");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<string> GenerateJsonReportAsync(HistoryRecord record,
        IReadOnlyList<LeftoverCandidate>? leftovers = null, CancellationToken ct = default)
    {
        var reportDir = GetReportDirectory();
        var fileName = $"uninstra-report-{record.OperationId}-{record.StartedAt:yyyyMMdd-HHmmss}.json";
        var path = Path.Combine(reportDir, fileName);

        var report = new
        {
            record.OperationId,
            OperationType = record.OperationType.ToString(),
            record.ApplicationName,
            record.Publisher,
            record.StartedAt,
            record.CompletedAt,
            Status = record.Status.ToString(),
            record.ExitCode,
            record.ItemsDetected,
            record.ItemsCleaned,
            record.ItemsSkipped,
            record.RecoveredBytes,
            record.RestorePointStatus,
            record.QuarantineAvailable,
            record.WarningCount,
            record.ErrorCount,
            Leftovers = leftovers?.Select(l => new
            {
                l.DisplayName,
                Type = l.Type.ToString(),
                l.Path,
                l.ConfidenceScore,
                ConfidenceLevel = l.ConfidenceLevel.ToString(),
                RiskLevel = l.RiskLevel.ToString(),
                l.Evidence,
                l.SizeBytes
            }).ToList(),
            GeneratedAt = DateTime.UtcNow,
            Generator = "Uninstra v1.0.0"
        };

        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
        _logger.LogInformation("JSON report generated: {Path}", path);
        return path;
    }

    public async Task<string> GenerateHtmlReportAsync(HistoryRecord record,
        IReadOnlyList<LeftoverCandidate>? leftovers = null, CancellationToken ct = default)
    {
        var reportDir = GetReportDirectory();
        var fileName = $"uninstra-report-{record.OperationId}-{record.StartedAt:yyyyMMdd-HHmmss}.html";
        var path = Path.Combine(reportDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>Uninstra Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,sans-serif;background:#1a1a2e;color:#e0e0e0;margin:2rem;}");
        sb.AppendLine("h1{color:#00bcd4;}h2{color:#80deea;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:1rem 0;}");
        sb.AppendLine("th,td{border:1px solid #333;padding:8px;text-align:left;}");
        sb.AppendLine("th{background:#16213e;color:#00bcd4;}tr:hover{background:#16213e;}");
        sb.AppendLine(".high{color:#4caf50;}.medium{color:#ff9800;}.low{color:#f44336;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>🛡 Uninstra Report</h1>");
        sb.AppendLine($"<p><strong>Operation:</strong> {record.OperationType}</p>");
        sb.AppendLine($"<p><strong>Application:</strong> {Escape(record.ApplicationName)}</p>");
        sb.AppendLine($"<p><strong>Publisher:</strong> {Escape(record.Publisher)}</p>");
        sb.AppendLine($"<p><strong>Status:</strong> {record.Status}</p>");
        sb.AppendLine($"<p><strong>Started:</strong> {record.StartedAt:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine($"<p><strong>Completed:</strong> {record.CompletedAt:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine($"<p><strong>Items Detected:</strong> {record.ItemsDetected} | <strong>Cleaned:</strong> {record.ItemsCleaned} | <strong>Skipped:</strong> {record.ItemsSkipped}</p>");
        sb.AppendLine($"<p><strong>Recovered:</strong> {FormatBytes(record.RecoveredBytes)}</p>");

        if (leftovers is { Count: > 0 })
        {
            sb.AppendLine("<h2>Leftovers</h2>");
            sb.AppendLine("<table><tr><th>Name</th><th>Type</th><th>Path</th><th>Confidence</th><th>Size</th><th>Evidence</th></tr>");
            foreach (var l in leftovers)
            {
                var levelClass = l.ConfidenceLevel.ToString().ToLower();
                sb.AppendLine($"<tr><td>{Escape(l.DisplayName)}</td><td>{l.Type}</td><td>{Escape(l.Path)}</td>");
                sb.AppendLine($"<td class=\"{levelClass}\">{l.ConfidenceScore} ({l.ConfidenceLevel})</td>");
                sb.AppendLine($"<td>{FormatBytes(l.SizeBytes)}</td>");
                sb.AppendLine($"<td>{string.Join("<br>", l.Evidence.Select(Escape))}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        sb.AppendLine($"<p style=\"margin-top:2rem;color:#666;\">Generated by Uninstra v1.0.0 at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("</body></html>");

        await File.WriteAllTextAsync(path, sb.ToString(), ct);
        _logger.LogInformation("HTML report generated: {Path}", path);
        return path;
    }

    private static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024.0:F1} KB",
        < 1073741824 => $"{bytes / 1048576.0:F1} MB",
        _ => $"{bytes / 1073741824.0:F2} GB"
    };
}
