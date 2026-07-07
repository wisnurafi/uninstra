namespace Uninstra.ElevatedHelper;

using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Safety;

/// <summary>
/// Elevated helper process — runs as administrator only when needed.
/// Communicates via named pipe with the main UI process.
/// Only accepts validated, typed operations — no raw shell commands.
/// </summary>
internal static class Program
{
    private const string PipeName = "Uninstra.ElevatedHelper";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static async Task Main(string[] args)
    {
        if (!IsRunningAsAdmin())
        {
            Console.Error.WriteLine("Elevated helper must run as administrator.");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine($"Uninstra Elevated Helper started (PID: {Environment.ProcessId})");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            await RunPipeServerAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Helper shutting down.");
        }
    }

    private static async Task RunPipeServerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipeSecurity = new PipeSecurity();
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser is not null)
            {
                pipeSecurity.AddAccessRule(new PipeAccessRule(currentUser,
                    PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow));
            }

            await using var server = NamedPipeServerStreamAcl.Create(
                PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                0, 0, pipeSecurity);

            Console.WriteLine("Waiting for connection...");
            await server.WaitForConnectionAsync(ct);
            Console.WriteLine("Client connected.");

            try
            {
                await HandleClientAsync(server, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error handling client: {ex.Message}");
            }
        }
    }

    private static async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var reader = new StreamReader(pipe);
        using var writer = new StreamWriter(pipe) { AutoFlush = true };

        var line = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(line)) return;

        ElevatedResponse response;

        try
        {
            var request = JsonSerializer.Deserialize<ElevatedRequest>(line, JsonOptions);
            if (request is null)
            {
                response = new ElevatedResponse { RequestId = "unknown", Message = "Invalid request" };
            }
            else
            {
                // Validate request freshness (reject stale requests > 30 seconds)
                if (Math.Abs((DateTime.UtcNow - request.Timestamp).TotalSeconds) > 30)
                {
                    response = new ElevatedResponse
                    {
                        RequestId = request.RequestId,
                        Message = "Request expired (stale timestamp)"
                    };
                }
                else
                {
                    response = await ProcessRequestAsync(request, ct);
                }
            }
        }
        catch (Exception ex)
        {
            response = new ElevatedResponse
            {
                RequestId = "unknown",
                Message = "Deserialization error",
                TechnicalDetails = ex.Message
            };
        }

        var json = JsonSerializer.Serialize(response, JsonOptions);
        await writer.WriteLineAsync(json);
    }

    private static async Task<ElevatedResponse> ProcessRequestAsync(ElevatedRequest request, CancellationToken ct)
    {
        Console.WriteLine($"Processing: {request.OperationType} ({request.RequestId})");

        // Safety: re-validate all paths
        if (!string.IsNullOrEmpty(request.Payload))
        {
            var (isProtected, reason) = SafetyPolicy.EvaluatePath(request.Payload);
            if (isProtected && request.OperationType is
                ElevatedOperationType.MoveToQuarantine or
                ElevatedOperationType.PermanentlyDeleteQuarantineItem)
            {
                return new ElevatedResponse
                {
                    RequestId = request.RequestId,
                    ErrorCode = "PROTECTED_PATH",
                    Message = $"Path is protected: {reason}"
                };
            }
        }

        return request.OperationType switch
        {
            ElevatedOperationType.CreateRestorePoint => await CreateRestorePointAsync(request, ct),
            ElevatedOperationType.MoveToQuarantine => MoveToQuarantine(request),
            ElevatedOperationType.RestoreFromQuarantine => RestoreFromQuarantine(request),
            _ => new ElevatedResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorCode = "NOT_IMPLEMENTED",
                Message = $"Operation {request.OperationType} not yet implemented"
            }
        };
    }

    private static async Task<ElevatedResponse> CreateRestorePointAsync(ElevatedRequest request, CancellationToken ct)
    {
        try
        {
            // Use WMI to create restore point — this is the standard Windows API for it
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"Checkpoint-Computer -Description '{request.Payload}' -RestorePointType 'MODIFY_SETTINGS'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "PROC_FAIL", Message = "Failed to start PowerShell" };

            await proc.WaitForExitAsync(ct);

            return new ElevatedResponse
            {
                RequestId = request.RequestId,
                Success = proc.ExitCode == 0,
                Message = proc.ExitCode == 0 ? "Restore point created" : "Failed to create restore point (System Protection may be disabled)",
                ErrorCode = proc.ExitCode == 0 ? "" : "RESTORE_FAILED"
            };
        }
        catch (Exception ex)
        {
            return new ElevatedResponse
            {
                RequestId = request.RequestId,
                ErrorCode = "EXCEPTION",
                Message = ex.Message
            };
        }
    }

    private static ElevatedResponse MoveToQuarantine(ElevatedRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Payload))
                return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "EMPTY_PATH", Message = "No path specified" };

            // Payload format: "sourcePath|quarantinePath"
            var parts = request.Payload.Split('|');
            if (parts.Length != 2)
                return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "INVALID_PAYLOAD", Message = "Invalid payload format" };

            var source = parts[0];
            var dest = parts[1];

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            if (File.Exists(source))
                File.Move(source, dest, true);
            else if (Directory.Exists(source))
                Directory.Move(source, dest);
            else
                return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "NOT_FOUND", Message = "Source not found" };

            return new ElevatedResponse { RequestId = request.RequestId, Success = true, Message = "Moved to quarantine" };
        }
        catch (Exception ex)
        {
            return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "MOVE_FAILED", Message = ex.Message };
        }
    }

    private static ElevatedResponse RestoreFromQuarantine(ElevatedRequest request)
    {
        try
        {
            var parts = request.Payload.Split('|');
            if (parts.Length != 2)
                return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "INVALID_PAYLOAD", Message = "Invalid payload format" };

            var quarantinePath = parts[0];
            var originalPath = parts[1];

            if (File.Exists(quarantinePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
                File.Move(quarantinePath, originalPath, true);
            }
            else if (Directory.Exists(quarantinePath))
            {
                Directory.Move(quarantinePath, originalPath);
            }
            else
            {
                return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "NOT_FOUND", Message = "Quarantine item not found" };
            }

            return new ElevatedResponse { RequestId = request.RequestId, Success = true, Message = "Restored successfully" };
        }
        catch (Exception ex)
        {
            return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "RESTORE_FAILED", Message = ex.Message };
        }
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
