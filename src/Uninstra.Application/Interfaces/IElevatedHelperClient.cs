namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Enums;
using Uninstra.Core.Models;

public interface IElevatedHelperClient
{
    Task<ElevatedResponse> SendAsync(ElevatedRequest request, CancellationToken ct = default);
    Task<bool> EnsureRunningAsync(CancellationToken ct = default);

    /// <summary>
    /// Ensures the helper is running, builds a fresh timestamped/nonce'd request
    /// bound to this session, sends it, and returns the response.
    /// </summary>
    Task<ElevatedResponse> ExecuteAsync(
        ElevatedOperationType operation, string payload, CancellationToken ct = default);
}
