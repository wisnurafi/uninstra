namespace Uninstra.Windows.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Parsing;
using Uninstra.Core.Results;

public sealed class UninstallService : IUninstallService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<UninstallService> _logger;

    public UninstallService(IProcessRunner processRunner, ILogger<UninstallService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<OperationResult<UninstallStatus>> UninstallAsync(
        InstalledApplication app, bool quiet = false,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var command = quiet && !string.IsNullOrWhiteSpace(app.QuietUninstallCommand)
            ? app.QuietUninstallCommand
            : app.UninstallCommand;

        if (string.IsNullOrWhiteSpace(command))
            return OperationResult.Failure<UninstallStatus>("NO_UNINSTALL", "No uninstall command found");

        var parsed = UninstallCommandParser.Parse(command);
        if (!parsed.IsValid)
            return OperationResult.Failure<UninstallStatus>("PARSE_ERROR", $"Invalid command: {parsed.ParseError}");

        progress?.Report($"Running uninstaller for {app.DisplayName}");

        // For MSI, use system msiexec
        if (parsed.IsMsiExec && parsed.MsiProductCode is not null)
        {
            var msiArgs = quiet
                ? $"/x{parsed.MsiProductCode} /qn /norestart"
                : $"/x{parsed.MsiProductCode}";

            var msiExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
            var result = await _processRunner.RunAsync(msiExe, msiArgs, progress: progress, ct: ct);
            if (!result.IsSuccess)
                return OperationResult.Failure<UninstallStatus>(result.Error!.Code, result.Error.Message);

            return EvaluateResult(result.Value, app);
        }

        // Standard exe uninstall
        if (!File.Exists(parsed.ExecutablePath))
        {
            _logger.LogWarning("Uninstaller not found: {Path}", parsed.ExecutablePath);
            return OperationResult.Failure<UninstallStatus>("EXE_MISSING", $"Uninstaller not found: {parsed.ExecutablePath}");
        }

        var runResult = await _processRunner.RunAsync(parsed.ExecutablePath, parsed.Arguments, progress: progress, ct: ct);
        if (!runResult.IsSuccess)
            return OperationResult.Failure<UninstallStatus>(runResult.Error!.Code, runResult.Error.Message);

        // Verify by checking if registry entry is gone
        progress?.Report("Verifying uninstall...");
        var verified = await VerifyUninstallAsync(app, ct);

        if (verified)
        {
            return OperationResult.Success<UninstallStatus>(UninstallStatus.Completed);
        }

        // Exit code 0 but entry still exists — could be incomplete
        return runResult.Value switch
        {
            0 => OperationResult.Success<UninstallStatus>(UninstallStatus.CompletedWithWarnings),
            1602 or 1223 => OperationResult.Success<UninstallStatus>(UninstallStatus.Cancelled),
            _ => OperationResult.Success<UninstallStatus>(UninstallStatus.UnknownResult)
        };
    }

    private OperationResult<UninstallStatus> EvaluateResult(int exitCode, InstalledApplication app)
    {
        return exitCode switch
        {
            0 => OperationResult.Success<UninstallStatus>(UninstallStatus.Completed),
            1602 or 1223 => OperationResult.Success<UninstallStatus>(UninstallStatus.Cancelled),
            1605 => OperationResult.Success<UninstallStatus>(UninstallStatus.Completed), // Product not found (already gone)
            3010 => OperationResult.Success<UninstallStatus>(UninstallStatus.CompletedWithWarnings), // Reboot required
            _ => OperationResult.Success<UninstallStatus>(UninstallStatus.UnknownResult)
        };
    }

    private Task<bool> VerifyUninstallAsync(InstalledApplication app, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            try
            {
                var hive = app.RegistryHive == RegistryHiveType.LocalMachine
                    ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                var view = app.RegistryView == RegistryViewType.Registry32
                    ? RegistryView.Registry32 : RegistryView.Registry64;

                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(app.RegistryKeyPath);
                return key is null; // Gone = success
            }
            catch { return false; }
        }, ct);
    }
}
