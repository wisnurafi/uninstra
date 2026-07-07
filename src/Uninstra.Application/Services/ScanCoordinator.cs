namespace Uninstra.Application.Services;

using Microsoft.Extensions.Logging;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed class ScanCoordinator
{
    private readonly IApplicationScanner _scanner;
    private readonly ILogger<ScanCoordinator> _logger;
    private List<InstalledApplication> _cachedApps = [];
    private DateTime _lastScan;

    public ScanCoordinator(IApplicationScanner scanner, ILogger<ScanCoordinator> logger)
    {
        _scanner = scanner;
        _logger = logger;
    }

    public IReadOnlyList<InstalledApplication> CachedApps => _cachedApps;

    public async Task<IReadOnlyList<InstalledApplication>> ScanAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _cachedApps.Count > 0 && DateTime.UtcNow - _lastScan < TimeSpan.FromMinutes(5))
            return _cachedApps;

        _logger.LogInformation("Starting application scan");
        var apps = await _scanner.ScanAsync(ct);
        _cachedApps = [.. apps];
        _lastScan = DateTime.UtcNow;
        _logger.LogInformation("Scan complete: {Count} applications found", apps.Count);
        return _cachedApps;
    }

    public void InvalidateCache() => _cachedApps.Clear();
}
