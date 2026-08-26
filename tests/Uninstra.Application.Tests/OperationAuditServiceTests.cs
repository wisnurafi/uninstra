namespace Uninstra.Application.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Uninstra.Application.Interfaces;
using Uninstra.Application.Services;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Xunit;

public class OperationAuditServiceTests
{
    private sealed class FakeHistoryRepository : IHistoryRepository
    {
        public List<HistoryRecord> Records { get; } = [];
        public int DeleteCalls;

        public Task AddAsync(HistoryRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public bool ThrowOnWrite { get; set; }

        public Task<IReadOnlyList<HistoryRecord>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HistoryRecord>>(Records.ToList());

        public Task<HistoryRecord?> GetByIdAsync(string operationId, CancellationToken ct = default) =>
            Task.FromResult<HistoryRecord?>(Records.FirstOrDefault(r => r.OperationId == operationId));

        public Task DeleteAsync(string operationId, CancellationToken ct = default)
        {
            DeleteCalls++;
            if (ThrowOnWrite) throw new InvalidOperationException("db offline");
            Records.RemoveAll(r => r.OperationId == operationId);
            return Task.CompletedTask;
        }
    }

    private static InstalledApplication MakeApp() => new()
    {
        Id = "app-1",
        DisplayName = "Test App",
        Publisher = "Acme Corp"
    };

    [Fact]
    public async Task StartAsync_WritesRecord_AndReturnsIt()
    {
        var repo = new FakeHistoryRepository();
        var svc = new OperationAuditService(repo, NullLogger<OperationAuditService>.Instance);

        var record = await svc.StartAsync(OperationType.DeepUninstall, MakeApp());

        record.OperationId.Should().NotBeNullOrWhiteSpace();
        record.OperationType.Should().Be(OperationType.DeepUninstall);
        record.ApplicationName.Should().Be("Test App");
        record.Publisher.Should().Be("Acme Corp");
        record.Status.Should().Be(UninstallStatus.UnknownResult);
        record.CompletedAt.Should().BeNull();
        repo.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task CompleteAsync_ReplacesRecord_WithFinalState()
    {
        var repo = new FakeHistoryRepository();
        var svc = new OperationAuditService(repo, NullLogger<OperationAuditService>.Instance);
        var record = await svc.StartAsync(OperationType.LeftoverCleanup, MakeApp());

        await svc.CompleteAsync(
            record, UninstallStatus.Completed,
            exitCode: 0,
            itemsDetected: 12,
            itemsCleaned: 10,
            itemsSkipped: 2,
            recoveredBytes: 4096);

        repo.Records.Should().ContainSingle("start record must be replaced, not duplicated");
        var final = repo.Records[0];
        final.Status.Should().Be(UninstallStatus.Completed);
        final.ExitCode.Should().Be(0);
        final.ItemsDetected.Should().Be(12);
        final.ItemsCleaned.Should().Be(10);
        final.ItemsSkipped.Should().Be(2);
        final.RecoveredBytes.Should().Be(4096);
        final.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_DoesNotThrow_WhenRepoFails()
    {
        var repo = new FakeHistoryRepository { ThrowOnWrite = true };
        var svc = new OperationAuditService(repo, NullLogger<OperationAuditService>.Instance);

        // Audit writes are best-effort: a history DB failure must never abort
        // the actual uninstall operation flowing through the service.
        var act = () => svc.StartAsync(OperationType.NormalUninstall, MakeApp());
        await act.Should().NotThrowAsync();
    }
}
