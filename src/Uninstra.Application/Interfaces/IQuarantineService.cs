namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;
using Uninstra.Core.Results;

public interface IQuarantineService
{
    Task<OperationResult> MoveToQuarantineAsync(LeftoverCandidate item, string operationId, string appName, CancellationToken ct = default);
    Task<OperationResult> RestoreAsync(QuarantineManifest manifest, CancellationToken ct = default);
    Task<OperationResult> PermanentDeleteAsync(QuarantineManifest manifest, CancellationToken ct = default);
    Task<IReadOnlyList<QuarantineManifest>> GetAllAsync(CancellationToken ct = default);
    Task CleanExpiredAsync(CancellationToken ct = default);
}
