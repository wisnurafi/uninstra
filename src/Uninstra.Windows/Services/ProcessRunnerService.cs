namespace Uninstra.Windows.Services;

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Results;

public sealed class ProcessRunnerService : IProcessRunner
{
    private readonly ILogger<ProcessRunnerService> _logger;

    public ProcessRunnerService(ILogger<ProcessRunnerService> logger) => _logger = logger;

    public async Task<OperationResult<int>> RunAsync(
        string executable, string arguments,
        bool waitForExit = true, TimeSpan? timeout = null,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(executable) && !executable.Contains(Path.DirectorySeparatorChar))
        {
            // Try system path
            var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), executable);
            if (File.Exists(sys)) executable = sys;
        }

        _logger.LogInformation("Running: {Exe} {Args}", executable, arguments);
        progress?.Report($"Starting: {Path.GetFileName(executable)}");

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = false, // Allow UI for interactive uninstallers
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                return OperationResult.Failure<int>("PROC_START", "Failed to start process");

            if (!waitForExit)
                return OperationResult.Success(0);

            var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(30);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(effectiveTimeout);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Process timed out: {Exe}", executable);
                return OperationResult.Failure<int>("TIMEOUT", "Process timed out");
            }

            _logger.LogInformation("Process exited: {Code}", process.ExitCode);
            progress?.Report($"Exited with code: {process.ExitCode}");
            return OperationResult.Success(process.ExitCode);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _logger.LogInformation("UAC cancelled by user");
            return OperationResult.Failure<int>("UAC_CANCELLED", "User cancelled UAC elevation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running process: {Exe}", executable);
            return OperationResult.Failure<int>("PROC_ERROR", ex.Message, ex.ToString());
        }
    }

    public Task<IReadOnlyList<(int Pid, string Name, string? MainModule)>> GetRelatedProcessesAsync(
        string installLocation, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var result = new List<(int, string, string?)>();
            if (string.IsNullOrWhiteSpace(installLocation)) return (IReadOnlyList<(int, string, string?)>)result;

            var normalized = Path.GetFullPath(installLocation).TrimEnd(Path.DirectorySeparatorChar);

            foreach (var proc in Process.GetProcesses())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var mainModule = proc.MainModule?.FileName;
                    if (mainModule is not null &&
                        mainModule.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add((proc.Id, proc.ProcessName, mainModule));
                    }
                }
                catch { /* Access denied for system processes */ }
                finally { proc.Dispose(); }
            }

            return (IReadOnlyList<(int, string, string?)>)result;
        }, ct);
    }

    public async Task<bool> TryCloseGracefullyAsync(int pid, CancellationToken ct = default)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            if (proc.CloseMainWindow())
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    await proc.WaitForExitAsync(cts.Token);
                    return true;
                }
                catch (OperationCanceledException) { return false; }
            }
            return false;
        }
        catch { return false; }
    }
}
