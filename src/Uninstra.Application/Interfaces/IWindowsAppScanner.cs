namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;

public interface IWindowsAppScanner
{
    Task<IReadOnlyList<WindowsApp>> ScanAsync(CancellationToken ct = default);
}
