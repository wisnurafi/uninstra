namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;

public interface IBrowserExtensionScanner
{
    Task<IReadOnlyList<BrowserExtension>> ScanAsync(CancellationToken ct = default);
}
