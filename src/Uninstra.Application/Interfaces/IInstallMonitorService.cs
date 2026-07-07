namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;
using Uninstra.Core.Results;

public interface IInstallMonitorService
{
    Task<OperationResult<InstallMonitorSession>> StartMonitoringAsync(
        string installerPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<InstallMonitorSession>> GetSessionsAsync(CancellationToken ct = default);
    Task<InstallMonitorSession?> GetSessionAsync(string sessionId, CancellationToken ct = default);
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}
