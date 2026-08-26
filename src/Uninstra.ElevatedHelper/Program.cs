namespace Uninstra.ElevatedHelper;

using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Safety;

/// <summary>
/// Elevated helper process — runs as administrator only when needed.
/// Communicates via named pipe with the main UI process.
/// Security model:
///  - pipe ACL restricted to the current user
///  - typed requests only (no shell strings from the client)
///  - timestamp freshness window (anti-replay)
///  - quarantine destinations are locked INSIDE the quarantine root
///  - service/task names are strictly validated (no injection)
/// </summary>
internal static class Program
{
    private const string PipeName = "Uninstra.ElevatedHelper";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string QuarantineRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Uninstra", "Quarantine");

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

        // Parent watchdog: the UI launches us with "--parent <pid>". If that
        // process disappears (crash, force-kill, normal exit while we idle),
        // shut down instead of lingering as an orphaned elevated process.
        var parentPid = ParseParentPid(args);
        if (parentPid > 0)
        {
            _ = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                        using var parent = System.Diagnostics.Process.GetProcessById(parentPid);
                        if (parent.HasExited) break;
                    }
                    catch (ArgumentException) { break; }   // PID no longer exists
                    catch { /* transient query failure — keep watching */ }
                }

                if (!cts.IsCancellationRequested)
                {
                    Console.WriteLine("Parent exited — shutting down.");
                    cts.Cancel();
                }
            }, cts.Token);
        }

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
            else if (Math.Abs((DateTime.UtcNow - request.Timestamp).TotalSeconds) > 30)
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

        return request.OperationType switch
        {
            ElevatedOperationType.CreateRestorePoint => await CreateRestorePointAsync(request, ct),
            ElevatedOperationType.MoveToQuarantine => MoveToQuarantine(request),
            ElevatedOperationType.RestoreFromQuarantine => RestoreFromQuarantine(request),
            ElevatedOperationType.PermanentlyDeleteQuarantineItem => PermanentDeleteQuarantineItem(request),
            ElevatedOperationType.DeleteRegistryKey => DeleteRegistryKey(request),
            ElevatedOperationType.DeleteRegistryValue => DeleteRegistryValue(request),
            ElevatedOperationType.StopService => RunServiceCommand(request, "stop"),
            ElevatedOperationType.DeleteService => RunServiceCommand(request, "delete"),
            ElevatedOperationType.DeleteScheduledTask => DeleteScheduledTask(request),
            _ => new ElevatedResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorCode = "NOT_IMPLEMENTED",
                Message = $"Operation {request.OperationType} not yet implemented"
            }
        };
    }

    // ────────────────────────────────────────────────────────────────
    //  Restore point
    // ────────────────────────────────────────────────────────────────

    private static async Task<ElevatedResponse> CreateRestorePointAsync(ElevatedRequest request, CancellationToken ct)
    {
        // The description is embedded in a PowerShell one-liner — strip anything
        // that could break out of the quoting context before it gets there.
        var description = SanitizeDescription(request.Payload);
        if (description.Length == 0) description = "Uninstra operation";

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null)
                return new ElevatedResponse { RequestId = request.RequestId, ErrorCode = "PROC_FAIL", Message = "Failed to start PowerShell" };

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

    private static string SanitizeDescription(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var forbidden = new[] { '\'', '"', '`', '$', ';', '|', '&', '<', '>', '\n', '\r', '{', '}' };
        foreach (var ch in forbidden)
            input = input.Replace(ch.ToString(), string.Empty);
        return input.Trim();
    }

    // ────────────────────────────────────────────────────────────────
    //  Quarantine moves — destination/source locked to quarantine root
    // ────────────────────────────────────────────────────────────────

    private static ElevatedResponse MoveToQuarantine(ElevatedRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Payload))
                return Fail(request, "EMPTY_PATH", "No path specified");

            var parts = SplitPayload(request.Payload);
            if (parts is null)
                return Fail(request, "INVALID_PAYLOAD", "Invalid payload format");

            var (source, dest) = parts.Value;

            // Hard rail: destination MUST live inside our quarantine root
            if (!IsInsideQuarantineRoot(dest))
                return Fail(request, "PROTECTED_PATH", "Destination is outside the quarantine area");

            // Source must exist and not be a protected system location
            var (isProtected, reason) = SafetyPolicy.EvaluatePath(source);
            if (isProtected)
                return Fail(request, "PROTECTED_PATH", $"Source is protected: {reason}");

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            if (File.Exists(source))
                File.Move(source, dest, true);
            else if (Directory.Exists(source))
                Directory.Move(source, dest);
            else
                return Fail(request, "NOT_FOUND", "Source not found");

            return Ok(request, "Moved to quarantine");
        }
        catch (Exception ex)
        {
            return Fail(request, "MOVE_FAILED", ex.Message);
        }
    }

    private static ElevatedResponse RestoreFromQuarantine(ElevatedRequest request)
    {
        try
        {
            var parts = SplitPayload(request.Payload);
            if (parts is null)
                return Fail(request, "INVALID_PAYLOAD", "Invalid payload format");

            var (quarantinePath, originalPath) = parts.Value;

            // BOTH sides validated: item may only leave the quarantine root,
            // and may only return to a location that is not system-protected.
            if (!IsInsideQuarantineRoot(quarantinePath))
                return Fail(request, "PROTECTED_PATH", "Source is outside the quarantine area");

            var (isProtected, reason) = SafetyPolicy.EvaluatePath(originalPath);
            if (isProtected)
                return Fail(request, "PROTECTED_PATH", $"Restore target is protected: {reason}");

            if (File.Exists(quarantinePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
                File.SetAttributes(quarantinePath, FileAttributes.Normal);
                File.Move(quarantinePath, originalPath, false);
            }
            else if (Directory.Exists(quarantinePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
                Directory.Move(quarantinePath, originalPath);
            }
            else
            {
                return Fail(request, "NOT_FOUND", "Quarantine item not found");
            }

            return Ok(request, "Restored successfully");
        }
        catch (Exception ex)
        {
            return Fail(request, "RESTORE_FAILED", ex.Message);
        }
    }

    private static ElevatedResponse PermanentDeleteQuarantineItem(ElevatedRequest request)
    {
        try
        {
            var target = request.Payload.Trim();
            if (!IsInsideQuarantineRoot(target))
                return Fail(request, "PROTECTED_PATH", "Refusing to delete outside the quarantine area");

            if (File.Exists(target))
            {
                File.SetAttributes(target, FileAttributes.Normal);
                File.Delete(target);
            }
            else if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
            else
            {
                return Fail(request, "NOT_FOUND", "Item not found");
            }

            return Ok(request, "Permanently deleted");
        }
        catch (Exception ex)
        {
            return Fail(request, "DELETE_FAILED", ex.Message);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Registry deletion (admin side)
    // ────────────────────────────────────────────────────────────────

    private static ElevatedResponse DeleteRegistryKey(ElevatedRequest request)
    {
        try
        {
            var parts = SplitPayload(request.Payload);
            var keyPath = parts?.Item1;
            if (string.IsNullOrWhiteSpace(keyPath))
                return Fail(request, "INVALID_PAYLOAD", "No registry path supplied");

            if (!IsSafeUserKeyPath(keyPath))
                return Fail(request, "PROTECTED_PATH", $"Refusing to delete protected key: {keyPath}");

            var hive = ResolveHive(keyPath, out var relativePath);

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            var parent = RegistryParent(relativePath);
            var leaf = RegistryLeaf(relativePath);
            if (parent is null || leaf.Length == 0)
                return Fail(request, "INVALID_PAYLOAD", "Malformed key path");

            using var parentKey = baseKey.OpenSubKey(parent, writable: true);
            if (parentKey is null) return Ok(request, "Already gone");
            parentKey.DeleteSubKeyTree(leaf, throwOnMissingSubKey: false);
            return Ok(request, "Registry key deleted");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Fail(request, "ACCESS_DENIED", ex.Message);
        }
        catch (Exception ex)
        {
            return Fail(request, "REGISTRY_FAILED", ex.Message);
        }
    }

    private static ElevatedResponse DeleteRegistryValue(ElevatedRequest request)
    {
        try
        {
            var parts = SplitPayload(request.Payload);
            var keyPath = parts?.Item1;
            var valueName = parts?.Item2;
            if (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrEmpty(valueName))
                return Fail(request, "INVALID_PAYLOAD", "Need keyPath|valueName");

            if (!IsSafeUserKeyPath(keyPath))
                return Fail(request, "PROTECTED_PATH", $"Refusing protected key: {keyPath}");

            var hive = ResolveHive(keyPath, out var relativePath);

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(relativePath, writable: true);
            if (key is null) return Ok(request, "Already gone");
            key.DeleteValue(valueName, throwOnMissingValue: false);
            return Ok(request, "Registry value deleted");
        }
        catch (Exception ex)
        {
            return Fail(request, "REGISTRY_FAILED", ex.Message);
        }
    }

    /// <summary>
    /// Whitelist approach: allowed roots are Software\<anything>, plus the
    /// Run/RunOnce startup keys. Everything else (Services, Windows NT,
    /// Policies, Setup...) is refused on the elevated side.
    /// </summary>
    private static bool IsSafeUserKeyPath(string fullPath)
    {
        var normalized = fullPath.Replace('/', '\\').Trim();

        foreach (var hivePrefix in new[] { @"HKLM\", @"HKCU\", @"HKEY_LOCAL_MACHINE\", @"HKEY_CURRENT_USER\" })
        {
            if (normalized.StartsWith(hivePrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[hivePrefix.Length..];
                break;
            }
        }

        var lower = normalized.ToLowerInvariant();

        if (lower.StartsWith(@"software\") && !lower.Contains(@"microsoft\windows\currentversion\policies"))
            return true;

        if (lower.StartsWith(@"software\microsoft\windows\currentversion\run")) return true;

        // Service keys are handled exclusively via sc.exe (StopService/DeleteService)
        return false;
    }

    private static RegistryHive ResolveHive(string fullPath, out string relativePath)
    {
        relativePath = fullPath.Replace('/', '\\').Trim();

        if (relativePath.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith(@"HKEY_LOCAL_MACHINE\", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase)
                ? relativePath[5..] : relativePath[@"HKEY_LOCAL_MACHINE\".Length..];
            return RegistryHive.LocalMachine;
        }

        if (relativePath.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith(@"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase)
                ? relativePath[5..] : relativePath[@"HKEY_CURRENT_USER\".Length..];
            return RegistryHive.CurrentUser;
        }

        // Default assumption from client contract: paths arrive without hive prefix → HKLM
        return RegistryHive.LocalMachine;
    }

    private static string? RegistryParent(string path)
    {
        var idx = path.LastIndexOf('\\');
        return idx <= 0 ? null : path[..idx];
    }

    private static string RegistryLeaf(string path)
    {
        var idx = path.LastIndexOf('\\');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    // ────────────────────────────────────────────────────────────────
    //  Services & scheduled tasks — strict name validation
    // ────────────────────────────────────────────────────────────────

    private static ElevatedResponse RunServiceCommand(ElevatedRequest request, string verb)
    {
        var serviceName = request.Payload.Trim();
        if (!IsValidServiceName(serviceName))
            return Fail(request, "INVALID_NAME", "Service name contains illegal characters");

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"{verb} \"{serviceName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(15000);
            return proc?.ExitCode == 0
                ? Ok(request, $"Service {verb} succeeded")
                : Fail(request, "SC_FAILED", $"sc.exe {verb} exited with {proc?.ExitCode}");
        }
        catch (Exception ex)
        {
            return Fail(request, "SERVICE_FAILED", ex.Message);
        }
    }

    private static ElevatedResponse DeleteScheduledTask(ElevatedRequest request)
    {
        var taskName = request.Payload.Trim();
        if (!IsValidTaskName(taskName))
            return Fail(request, "INVALID_NAME", "Task name contains illegal characters");

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN \"{taskName}\" /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(15000);
            return proc?.ExitCode == 0
                ? Ok(request, "Scheduled task deleted")
                : Fail(request, "SCHTASKS_FAILED", $"schtasks exited with {proc?.ExitCode}");
        }
        catch (Exception ex)
        {
            return Fail(request, "TASK_FAILED", ex.Message);
        }
    }

    /// <summary>sc.exe names: letters, digits, space, underscore, dash, dot — nothing else.</summary>
    private static bool IsValidServiceName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 256 &&
        name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '.');

    /// <summary>schtasks /TN allows folder separators but never metacharacters.</summary>
    private static bool IsValidTaskName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 512 &&
        !name.Any(c => c is '"' or '\'' or '&' or '|' or '<' or '>' or ';' or '%' or '`' or '$');

    // ────────────────────────────────────────────────────────────────
    //  Shared helpers
    // ────────────────────────────────────────────────────────────────

    private static (string Item1, string Item2)? SplitPayload(string payload)
    {
        var idx = payload.IndexOf('|');
        return idx <= 0 || idx == payload.Length - 1
            ? null
            : (payload[..idx].Trim(), payload[(idx + 1)..].Trim());
    }

    private static bool IsInsideQuarantineRoot(string candidate)
    {
        try
        {
            var root = Path.GetFullPath(QuarantineRoot)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(candidate);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static ElevatedResponse Ok(ElevatedRequest request, string message) =>
        new() { RequestId = request.RequestId, Success = true, Message = message };

    private static ElevatedResponse Fail(ElevatedRequest request, string code, string message) =>
        new() { RequestId = request.RequestId, Success = false, ErrorCode = code, Message = message };

    private static int ParseParentPid(string[] args)
    {
        var idx = Array.IndexOf(args, "--parent");
        return idx >= 0 && idx + 1 < args.Length &&
               int.TryParse(args[idx + 1], out var pid)
            ? pid
            : 0;
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
