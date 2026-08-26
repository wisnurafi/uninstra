namespace Uninstra.IntegrationTests;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Infrastructure.Data;
using Xunit;

/// <summary>
/// Real SQLite round trips against a temporary database file — verifies schema
/// migrations, enum persistence, nullable column handling and delete semantics.
/// </summary>
public class SqliteHistoryRoundTripTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDatabaseService _db;
    private readonly SqliteHistoryRepository _repo;

    public SqliteHistoryRoundTripTests()
    {
        _dbPath = Path.Combine(
            Path.GetTempPath(),
            $"uninstra-itest-{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(
            NullLogger<SqliteDatabaseService>.Instance, _dbPath);
        _repo = new SqliteHistoryRepository(
            _db, NullLogger<SqliteHistoryRepository>.Instance);
    }

    private static HistoryRecord MakeRecord(string opId, bool completed) => new()
    {
        OperationId = opId,
        OperationType = OperationType.DeepUninstall,
        ApplicationId = "app-1",
        ApplicationName = "Round Trip App",
        Publisher = "Acme",
        StartedAt = DateTime.UtcNow.AddMinutes(-5),
        CompletedAt = completed ? DateTime.UtcNow : null,
        Status = completed ? UninstallStatus.Completed : UninstallStatus.UnknownResult,
        ExitCode = completed ? 0 : null,
        ItemsDetected = 7,
        ItemsCleaned = 6,
        ItemsSkipped = 1,
        RecoveredBytes = 123456,
        RestorePointStatus = completed ? "created" : "",
        QuarantineAvailable = true,
        WarningCount = 1,
        ErrorCount = 0,
        ReportPath = completed ? @"C:\temp\report.html" : ""
    };

    [Fact]
    public async Task AddThenGetById_PreservesAllFields()
    {
        await _db.InitializeAsync();
        var original = MakeRecord($"op-{Guid.NewGuid():N}".Replace("-", "")[..16], completed: true);

        await _repo.AddAsync(original);
        var loaded = await _repo.GetByIdAsync(original.OperationId);

        loaded.Should().NotBeNull();
        loaded!.OperationId.Should().Be(original.OperationId);
        loaded.OperationType.Should().Be(original.OperationType);
        loaded.ApplicationName.Should().Be(original.ApplicationName);
        loaded.Publisher.Should().Be(original.Publisher);
        loaded.Status.Should().Be(UninstallStatus.Completed);
        loaded.ExitCode.Should().Be(0);
        loaded.ItemsDetected.Should().Be(7);
        loaded.ItemsCleaned.Should().Be(6);
        loaded.ItemsSkipped.Should().Be(1);
        loaded.RecoveredBytes.Should().Be(123456);
        loaded.RestorePointStatus.Should().Be("created");
        loaded.QuarantineAvailable.Should().BeTrue();
        loaded.WarningCount.Should().Be(1);
        loaded.ErrorCount.Should().Be(0);
        loaded.ReportPath.Should().Be(@"C:\temp\report.html");
        loaded.CompletedAt.Should().NotBeNull();
        loaded.StartedAt.Should().BeCloseTo(original.StartedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task NullableColumns_SurviveNullRoundTrip()
    {
        await _db.InitializeAsync();
        var pending = MakeRecord($"op-{Guid.NewGuid():N}".Replace("-", "")[..16], completed: false);

        await _repo.AddAsync(pending);
        var loaded = await _repo.GetByIdAsync(pending.OperationId);

        loaded!.CompletedAt.Should().BeNull();
        loaded.ExitCode.Should().BeNull();
        loaded.Status.Should().Be(UninstallStatus.UnknownResult);
    }

    [Fact]
    public async Task GetAll_ReturnsNewestFirst_AndDeleteRemoves()
    {
        await _db.InitializeAsync();
        var older = MakeRecord("op-older-000000001", completed: false);
        older = older with { StartedAt = DateTime.UtcNow.AddHours(-2) };
        var newer = MakeRecord("op-newer-000000001", completed: true);

        await _repo.AddAsync(older);
        await _repo.AddAsync(newer);

        var all = await _repo.GetAllAsync();
        all.Should().HaveCountGreaterThanOrEqualTo(2);
        all[0].StartedAt.Should().BeOnOrAfter(all[^1].StartedAt);

        await _repo.DeleteAsync(newer.OperationId);
        var gone = await _repo.GetByIdAsync(newer.OperationId);
        gone.Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
