namespace Uninstra.Infrastructure.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Uninstra.Application.Interfaces;
using System.Data;

public sealed class SqliteDatabaseService : IDatabaseService, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDatabaseService> _logger;
    private SqliteConnection? _connection;

    public SqliteDatabaseService(ILogger<SqliteDatabaseService> logger)
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Uninstra");
        Directory.CreateDirectory(appDataPath);

        var dbPath = Path.Combine(appDataPath, "uninstra.db");
        _connectionString = $"Data Source={dbPath}";
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync(ct);

        _logger.LogInformation("Database initialized at {Path}", _connectionString);

        await ExecuteMigrationAsync(ct);
    }

    private async Task ExecuteMigrationAsync(CancellationToken ct)
    {
        var sql = """
            CREATE TABLE IF NOT EXISTS History (
                OperationId TEXT PRIMARY KEY,
                OperationType INTEGER NOT NULL,
                ApplicationId TEXT,
                ApplicationName TEXT,
                Publisher TEXT,
                StartedAt TEXT NOT NULL,
                CompletedAt TEXT,
                Status INTEGER NOT NULL,
                ExitCode INTEGER,
                ItemsDetected INTEGER DEFAULT 0,
                ItemsCleaned INTEGER DEFAULT 0,
                ItemsSkipped INTEGER DEFAULT 0,
                RecoveredBytes INTEGER DEFAULT 0,
                RestorePointStatus TEXT,
                QuarantineAvailable INTEGER DEFAULT 0,
                WarningCount INTEGER DEFAULT 0,
                ErrorCount INTEGER DEFAULT 0,
                ReportPath TEXT
            );

            CREATE TABLE IF NOT EXISTS QuarantineManifests (
                OperationId TEXT NOT NULL,
                ApplicationId TEXT NOT NULL,
                ApplicationName TEXT,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                OriginalPath TEXT NOT NULL,
                QuarantinePath TEXT NOT NULL,
                Size INTEGER DEFAULT 0,
                Hash TEXT,
                ItemType INTEGER NOT NULL,
                RegistryBackup TEXT,
                CanRestore INTEGER DEFAULT 1,
                RestoreWarnings TEXT,
                PRIMARY KEY (OperationId, OriginalPath)
            );

            CREATE TABLE IF NOT EXISTS InstallMonitorSessions (
                SessionId TEXT PRIMARY KEY,
                InstallerPath TEXT,
                InstallerHash TEXT,
                InstallerPublisher TEXT,
                StartedAt TEXT NOT NULL,
                CompletedAt TEXT,
                RootProcessId INTEGER,
                ChildProcesses TEXT,
                CreatedFiles TEXT,
                ModifiedFiles TEXT,
                CreatedDirectories TEXT,
                RegistryChanges TEXT,
                NewServices TEXT,
                NewScheduledTasks TEXT,
                NewStartupEntries TEXT,
                DetectedApplications TEXT,
                Warnings TEXT,
                IncompleteMonitoringReason TEXT
            );

            CREATE TABLE IF NOT EXISTS AppCache (
                Id TEXT PRIMARY KEY,
                Data TEXT NOT NULL,
                CachedAt TEXT NOT NULL
            );
            """;

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<T> ExecuteAsync<T>(Func<IDbConnection, Task<T>> action, CancellationToken ct = default)
    {
        if (_connection is null || _connection.State != ConnectionState.Open)
            await InitializeAsync(ct);

        return await action(_connection!);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
