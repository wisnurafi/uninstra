namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;

public interface IApplicationScanner
{
    Task<IReadOnlyList<InstalledApplication>> ScanAsync(CancellationToken ct = default);
}
