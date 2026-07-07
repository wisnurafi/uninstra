namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;
using Uninstra.Core.Results;

public interface IJunkScanner
{
    Task<IReadOnlyList<JunkCategory>> ScanAsync(CancellationToken ct = default);
    Task<OperationResult> CleanAsync(IReadOnlyList<JunkItem> items, CancellationToken ct = default);
}
