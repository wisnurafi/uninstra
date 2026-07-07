namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Results;

public interface IUninstallService
{
    Task<OperationResult<UninstallStatus>> UninstallAsync(
        InstalledApplication app,
        bool quiet = false,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
