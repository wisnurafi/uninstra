namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Results;

public interface IProcessRunner
{
    Task<OperationResult<int>> RunAsync(
        string executable, string arguments,
        bool waitForExit = true,
        TimeSpan? timeout = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<(int Pid, string Name, string? MainModule)>> GetRelatedProcessesAsync(
        string installLocation, CancellationToken ct = default);

    Task<bool> TryCloseGracefullyAsync(int pid, CancellationToken ct = default);
}
