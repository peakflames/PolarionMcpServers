namespace PolarionRemoteMcpServer.Rbac.Caching;

/// <summary>Non-generic seam <see cref="RbacCacheJanitor"/> sweeps over — every RBAC
/// <see cref="TtlCache{TKey,TValue}"/> instance registers itself under this interface too.</summary>
public interface IEvictableCache
{
    ValueTask<int> EvictExpiredAsync(CancellationToken cancellationToken = default);
}
