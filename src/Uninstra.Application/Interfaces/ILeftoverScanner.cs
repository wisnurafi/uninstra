namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;

public interface ILeftoverScanner
{
    Task<IReadOnlyList<LeftoverCandidate>> ScanAsync(InstalledApplication app, CancellationToken ct = default);
}
