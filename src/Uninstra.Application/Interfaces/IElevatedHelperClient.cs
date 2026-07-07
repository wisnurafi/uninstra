namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;

public interface IElevatedHelperClient
{
    Task<ElevatedResponse> SendAsync(ElevatedRequest request, CancellationToken ct = default);
    Task<bool> EnsureRunningAsync(CancellationToken ct = default);
}
