using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PolarionRemoteMcpServer.Rbac.Caching;

/// <summary>
/// Periodically evicts expired entries from every registered <see cref="IEvictableCache"/>. Uses
/// <see cref="PeriodicTimer"/>(period, timeProvider), never <c>Task.Delay</c> or
/// <c>DateTime.UtcNow</c>, so a fake <see cref="TimeProvider"/> can drive it deterministically.
/// </summary>
public sealed class RbacCacheJanitor : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(30);

    private readonly IReadOnlyList<IEvictableCache> _caches;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RbacCacheJanitor> _logger;

    public RbacCacheJanitor(
        IEnumerable<IEvictableCache> caches, TimeProvider timeProvider, ILogger<RbacCacheJanitor> logger)
    {
        _caches = caches.ToArray();
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        var evicted = 0;
        foreach (var cache in _caches)
        {
            evicted += await cache.EvictExpiredAsync(ct);
        }

        _logger.LogDebug(
            "RBAC cache janitor evicted {Count} expired entries across {CacheCount} caches.", evicted, _caches.Count);
    }
}
