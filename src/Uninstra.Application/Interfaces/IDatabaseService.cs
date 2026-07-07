namespace Uninstra.Application.Interfaces;

public interface IDatabaseService
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<T> ExecuteAsync<T>(Func<System.Data.IDbConnection, Task<T>> action, CancellationToken ct = default);
}
