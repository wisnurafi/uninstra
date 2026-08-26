namespace Uninstra.Windows.Services;

using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;

/// <summary>
/// Client for the elevated helper process. Communicates over a named pipe
/// restricted to the current user. Launches the helper on demand (triggers UAC),
/// sends typed requests with session id + nonce (anti-replay), rejects stale responses.
/// </summary>
public sealed class ElevatedHelperClient : IElevatedHelperClient
{
    private const string PipeName = "Uninstra.ElevatedHelper";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<ElevatedHelperClient> _logger;
    private readonly string _sessionId;
    private readonly SemaphoreSlim _pipeLock = new(1, 1);

    public ElevatedHelperClient(ILogger<ElevatedHelperClient> logger)
    {
        _logger = logger;
        _sessionId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    }

    public async Task<bool> EnsureRunningAsync(CancellationToken ct = default)
    {
        // Fast path: helper already listening?
        if (await TryConnectAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false))
            return true;

        var helperPath = GetHelperPath();
        if (string.IsNullOrEmpty(helperPath) || !File.Exists(helperPath))
        {
            _logger.LogWarning("Elevated helper executable not found at {Path}", helperPath);
            return false;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = helperPath,
                UseShellExecute = true,
                Verb = "runas", // triggers UAC elevation prompt
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                Arguments = $"--session {_sessionId} --parent {Environment.ProcessId}"
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User declined or failed to launch elevated helper");
            return false;
        }

        // Wait for the pipe to come up (UAC approval + startup)
        for (int attempt = 0; attempt < 20; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct).ConfigureAwait(false);
            if (await TryConnectAsync(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false))
                return true;
        }

        _logger.LogWarning("Elevated helper did not start within timeout");
        return false;
    }

    public async Task<ElevatedResponse> SendAsync(ElevatedRequest request, CancellationToken ct = default)
    {
        await _pipeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // Direct connect with short retries — avoids consuming the helper's
            // single-instance pipe with a throwaway probe connection.
            var connected = false;
            for (int attempt = 0; attempt < 3 && !connected; attempt++)
            {
                try
                {
                    await pipe.ConnectAsync(4000, ct).ConfigureAwait(false);
                    connected = pipe.IsConnected;
                }
                catch (TimeoutException)
                {
                    connected = false;
                }
            }

            if (!connected)
            {
                return new ElevatedResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    ErrorCode = "HELPER_UNAVAILABLE",
                    Message = "Could not connect to the elevated helper process"
                };
            }

            // Disposal order matters: nested disposers run in REVERSE declaration
            // order. We declare both wrappers with leaveOpen and tear them down
            // EXPLICITLY in a finally (writer first while the pipe is alive, then
            // reader) so no dispose-time flush can ever throw past this method —
            // neither on the success path nor when the request itself failed.
            // (The original code declared the writer first, so unwinding disposed
            // the reader before the writer: the reader closed the pipe and the
            // writer's final flush threw ObjectDisposedException
            // "Cannot access a closed pipe" on every completed request.)
            var reader = new StreamReader(pipe, leaveOpen: true);
            var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            try
            {
                var json = JsonSerializer.Serialize(request, JsonOptions);
                await writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);

                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(line))
                {
                    return new ElevatedResponse
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        ErrorCode = "EMPTY_RESPONSE",
                        Message = "Helper closed the connection without a response"
                    };
                }

                return JsonSerializer.Deserialize<ElevatedResponse>(line, JsonOptions)
                    ?? new ElevatedResponse
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        ErrorCode = "DESERIALIZE_ERROR",
                        Message = "Invalid response from helper"
                    };
            }
            finally
            {
                try { await writer.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogDebug(ex, "Pipe writer teardown warning"); }
                try { reader.Dispose(); }
                catch (Exception ex) { _logger.LogDebug(ex, "Pipe reader teardown warning"); }
            }
        }
        finally
        {
            _pipeLock.Release();
        }
    }

    /// <summary>Convenience wrapper: ensures the helper runs, builds a signed-timestamp request and sends it.</summary>
    public async Task<ElevatedResponse> ExecuteAsync(
        ElevatedOperationType operation, string payload, CancellationToken ct = default)
    {
        if (!await EnsureRunningAsync(ct).ConfigureAwait(false))
        {
            return new ElevatedResponse
            {
                RequestId = NewRequestId(),
                Success = false,
                ErrorCode = "HELPER_UNAVAILABLE",
                Message = "Elevation was declined or the helper could not be started"
            };
        }

        var request = new ElevatedRequest
        {
            RequestId = NewRequestId(),
            SessionId = _sessionId,
            Nonce = GenerateNonce(),
            OperationType = operation,
            Payload = payload,
            Timestamp = DateTime.UtcNow
        };

        return await SendAsync(request, ct).ConfigureAwait(false);
    }

    private async Task<bool> TryConnectAsync(TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            using var probe = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await probe.ConnectAsync((int)timeout.TotalMilliseconds, ct).ConfigureAwait(false);
            return probe.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetHelperPath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "Uninstra.ElevatedHelper.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string NewRequestId() => Guid.NewGuid().ToString("N")[..16];

    private static string GenerateNonce() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
}
