namespace Uninstra.Infrastructure.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Data;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;

public sealed class SqliteHistoryRepository : IHistoryRepository
{
    private readonly IDatabaseService _db;
    private readonly ILogger<SqliteHistoryRepository> _logger;

    public SqliteHistoryRepository(IDatabaseService db, ILogger<SqliteHistoryRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddAsync(HistoryRecord record, CancellationToken ct = default)
    {
        await _db.ExecuteAsync<int>(async conn =>
        {
            using var cmd = ((SqliteConnection)conn).CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO History
                (OperationId,OperationType,ApplicationId,ApplicationName,Publisher,
                 StartedAt,CompletedAt,Status,ExitCode,ItemsDetected,ItemsCleaned,
                 ItemsSkipped,RecoveredBytes,RestorePointStatus,QuarantineAvailable,
                 WarningCount,ErrorCount,ReportPath)
                VALUES (@oid,@otype,@aid,@aname,@pub,@start,@end,@status,@exit,
                        @detected,@cleaned,@skipped,@recovered,@restore,@quarantine,
                        @warnings,@errors,@report)
                """;
            cmd.Parameters.AddWithValue("@oid", record.OperationId);
            cmd.Parameters.AddWithValue("@otype", (int)record.OperationType);
            cmd.Parameters.AddWithValue("@aid", record.ApplicationId);
            cmd.Parameters.AddWithValue("@aname", record.ApplicationName);
            cmd.Parameters.AddWithValue("@pub", record.Publisher);
            cmd.Parameters.AddWithValue("@start", record.StartedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@end", (object?)record.CompletedAt?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", (int)record.Status);
            cmd.Parameters.AddWithValue("@exit", (object?)record.ExitCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@detected", record.ItemsDetected);
            cmd.Parameters.AddWithValue("@cleaned", record.ItemsCleaned);
            cmd.Parameters.AddWithValue("@skipped", record.ItemsSkipped);
            cmd.Parameters.AddWithValue("@recovered", record.RecoveredBytes);
            cmd.Parameters.AddWithValue("@restore", record.RestorePointStatus);
            cmd.Parameters.AddWithValue("@quarantine", record.QuarantineAvailable ? 1 : 0);
            cmd.Parameters.AddWithValue("@warnings", record.WarningCount);
            cmd.Parameters.AddWithValue("@errors", record.ErrorCount);
            cmd.Parameters.AddWithValue("@report", record.ReportPath);
            return await cmd.ExecuteNonQueryAsync(ct);
        }, ct);
    }

    public async Task<IReadOnlyList<HistoryRecord>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.ExecuteAsync(async conn =>
        {
            using var cmd = ((SqliteConnection)conn).CreateCommand();
            cmd.CommandText = "SELECT * FROM History ORDER BY StartedAt DESC";
            using var reader = await cmd.ExecuteReaderAsync(ct);
            var records = new List<HistoryRecord>();
            while (await reader.ReadAsync(ct))
            {
                records.Add(MapRecord(reader));
            }
            return (IReadOnlyList<HistoryRecord>)records;
        }, ct);
    }

    public async Task<HistoryRecord?> GetByIdAsync(string operationId, CancellationToken ct = default)
    {
        return await _db.ExecuteAsync(async conn =>
        {
            using var cmd = ((SqliteConnection)conn).CreateCommand();
            cmd.CommandText = "SELECT * FROM History WHERE OperationId = @id";
            cmd.Parameters.AddWithValue("@id", operationId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? MapRecord(reader) : null;
        }, ct);
    }

    public async Task DeleteAsync(string operationId, CancellationToken ct = default)
    {
        await _db.ExecuteAsync<int>(async conn =>
        {
            using var cmd = ((SqliteConnection)conn).CreateCommand();
            cmd.CommandText = "DELETE FROM History WHERE OperationId = @id";
            cmd.Parameters.AddWithValue("@id", operationId);
            return await cmd.ExecuteNonQueryAsync(ct);
        }, ct);
    }

    private static HistoryRecord MapRecord(IDataReader reader) => new()
    {
        OperationId = reader.GetString(reader.GetOrdinal("OperationId")),
        OperationType = (OperationType)reader.GetInt32(reader.GetOrdinal("OperationType")),
        ApplicationId = reader.GetString(reader.GetOrdinal("ApplicationId")),
        ApplicationName = reader.GetString(reader.GetOrdinal("ApplicationName")),
        Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
        StartedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt"))),
        CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))),
        Status = (UninstallStatus)reader.GetInt32(reader.GetOrdinal("Status")),
        ExitCode = reader.IsDBNull(reader.GetOrdinal("ExitCode")) ? null : reader.GetInt32(reader.GetOrdinal("ExitCode")),
        ItemsDetected = reader.GetInt32(reader.GetOrdinal("ItemsDetected")),
        ItemsCleaned = reader.GetInt32(reader.GetOrdinal("ItemsCleaned")),
        ItemsSkipped = reader.GetInt32(reader.GetOrdinal("ItemsSkipped")),
        RecoveredBytes = reader.GetInt64(reader.GetOrdinal("RecoveredBytes")),
        RestorePointStatus = reader.GetString(reader.GetOrdinal("RestorePointStatus")),
        QuarantineAvailable = reader.GetInt32(reader.GetOrdinal("QuarantineAvailable")) == 1,
        WarningCount = reader.GetInt32(reader.GetOrdinal("WarningCount")),
        ErrorCount = reader.GetInt32(reader.GetOrdinal("ErrorCount")),
        ReportPath = reader.GetString(reader.GetOrdinal("ReportPath"))
    };
}
