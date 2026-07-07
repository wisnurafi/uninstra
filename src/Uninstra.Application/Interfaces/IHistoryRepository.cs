namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;

public interface IHistoryRepository
{
    Task AddAsync(HistoryRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<HistoryRecord>> GetAllAsync(CancellationToken ct = default);
    Task<HistoryRecord?> GetByIdAsync(string operationId, CancellationToken ct = default);
    Task DeleteAsync(string operationId, CancellationToken ct = default);
}
