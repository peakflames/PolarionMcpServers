using Microsoft.Extensions.Logging;

namespace PolarionRemoteMcpServer.Rbac.Caching;

/// <summary>
/// A capacity-bounded, TTL-expiring, single-flight cache: <c>Dictionary</c> + <see cref="System.Threading.Lock"/>,
/// capacity from options, <see cref="TimeProvider"/> for every time read, with concurrent identical
/// requests for a key in flight sharing one upstream call.
///
/// In-flight requests are coalesced via <see cref="TaskCompletionSource{TResult}"/>, not
/// <see cref="Lazy{T}"/> and not a bare <c>Task&lt;T&gt;</c>. A bare <c>Task&lt;T&gt;</c> would need the
/// factory invoked inside the lock; <c>Lazy&lt;T&gt;</c> caches synchronous throws and has documented
/// re-entrancy deadlocks. Both would also bind the shared task to the *first* caller's
/// <see cref="CancellationToken"/> — a real correctness bug, since caller A disconnecting mid-flight
/// would hand callers B and C an <see cref="OperationCanceledException"/> for a call that has nothing
/// to do with them. With a TCS, the factory runs with <see cref="CancellationToken.None"/> and every
/// caller (including the one that started the fetch) independently does
/// <c>Task.WaitAsync(callerToken)</c>.
///
/// Never caches a fault: on a thrown exception, the entry is removed before the exception is
/// rethrown to every waiter — the value type is always the plain upstream fact (never a decision
/// object), so there is no representable "cached error" state for someone to get wrong later.
///
/// Saturation refuses the insert and serves the caller uncached — a cache refusal must never deny a
/// user. An opportunistic expired-entry sweep runs on the saturation path before giving up, and a
/// rate-limited warning names the capacity knob so an operator can raise it.
/// </summary>
public sealed class TtlCache<TKey, TValue> : IEvictableCache
    where TKey : notnull
{
    private static readonly TimeSpan SaturationWarningInterval = TimeSpan.FromSeconds(60);

    private readonly Dictionary<TKey, Entry> _entries;
    private readonly Lock _lock = new();
    private readonly int _capacity;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly string _cacheName;
    private DateTimeOffset _lastSaturationWarning = DateTimeOffset.MinValue;

    public TtlCache(
        int capacity,
        TimeProvider timeProvider,
        ILogger logger,
        string cacheName,
        IEqualityComparer<TKey>? comparer = null)
    {
        _capacity = capacity;
        _timeProvider = timeProvider;
        _logger = logger;
        _cacheName = cacheName;
        _entries = new Dictionary<TKey, Entry>(comparer);
    }

    public int Count
    {
        get { lock (_lock) { return _entries.Count; } }
    }

    /// <summary>Fixed-TTL convenience overload — every entry expires <paramref name="ttl"/> after
    /// being populated.</summary>
    public ValueTask<TValue> GetOrAddAsync(
        TKey key, TimeSpan ttl, Func<CancellationToken, Task<TValue>> factory, CancellationToken cancellationToken) =>
        GetOrAddAsync(key, async ct => (await factory(ct).ConfigureAwait(false), ttl), cancellationToken);

    /// <summary>Per-entry-TTL overload — the factory decides how long its own result should live (e.g.
    /// the identity cache's shorter TTL for a negative/not-found outcome).</summary>
    public async ValueTask<TValue> GetOrAddAsync(
        TKey key,
        Func<CancellationToken, Task<(TValue Value, TimeSpan Ttl)>> factory,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        TaskCompletionSource<TValue>? pending;
        var startFactory = false;

        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing.HasValue && existing.ExpiresAt > now)
                {
                    return existing.Value!;
                }

                if (existing.Pending is not null)
                {
                    pending = existing.Pending;
                }
                else
                {
                    pending = existing.Pending = NewTcs();
                    existing.HasValue = false;
                    startFactory = true;
                }
            }
            else if (TryReserveSlotLocked(now))
            {
                pending = NewTcs();
                _entries[key] = new Entry { Pending = pending };
                startFactory = true;
            }
            else
            {
                pending = null;
            }
        }

        if (pending is null)
        {
            // Saturated: refuse the insert, serve this caller uncached rather than deny them.
            var (uncachedValue, _) = await factory(cancellationToken).ConfigureAwait(false);
            return uncachedValue;
        }

        if (startFactory)
        {
            _ = RunFactoryAsync(key, pending, factory);
        }

        return await pending.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Set(TKey key, TValue value, TimeSpan ttl)
    {
        lock (_lock)
        {
            _entries[key] = new Entry
            {
                HasValue = true,
                Value = value,
                ExpiresAt = _timeProvider.GetUtcNow() + ttl,
            };
        }
    }

    public ValueTask<int> EvictExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        int evicted;
        lock (_lock)
        {
            evicted = EvictExpiredLocked(now);
        }
        return ValueTask.FromResult(evicted);
    }

    private static TaskCompletionSource<TValue> NewTcs() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task RunFactoryAsync(
        TKey key, TaskCompletionSource<TValue> pending, Func<CancellationToken, Task<(TValue Value, TimeSpan Ttl)>> factory)
    {
        try
        {
            // Deliberately CancellationToken.None: the fetch is shared by every waiter, so no single
            // caller's disconnect should cancel it out from under the others.
            var (value, ttl) = await factory(CancellationToken.None).ConfigureAwait(false);

            lock (_lock)
            {
                if (_entries.TryGetValue(key, out var entry) && ReferenceEquals(entry.Pending, pending))
                {
                    entry.HasValue = true;
                    entry.Value = value;
                    entry.ExpiresAt = _timeProvider.GetUtcNow() + ttl;
                    entry.Pending = null;
                }
            }

            pending.SetResult(value);
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(key, out var entry) && ReferenceEquals(entry.Pending, pending))
                {
                    _entries.Remove(key);
                }
            }

            pending.SetException(ex);
        }
    }

    /// <summary>Called with <see cref="_lock"/> already held. Tries an opportunistic expired-entry
    /// sweep before declaring the cache saturated.</summary>
    private bool TryReserveSlotLocked(DateTimeOffset now)
    {
        if (_entries.Count < _capacity)
        {
            return true;
        }

        EvictExpiredLocked(now);

        if (_entries.Count < _capacity)
        {
            return true;
        }

        WarnSaturatedLocked(now);
        return false;
    }

    private int EvictExpiredLocked(DateTimeOffset now)
    {
        List<TKey>? expired = null;

        foreach (var (key, entry) in _entries)
        {
            if (entry.Pending is null && entry.HasValue && entry.ExpiresAt <= now)
            {
                (expired ??= []).Add(key);
            }
        }

        if (expired is null)
        {
            return 0;
        }

        foreach (var key in expired)
        {
            _entries.Remove(key);
        }

        return expired.Count;
    }

    private void WarnSaturatedLocked(DateTimeOffset now)
    {
        if (now - _lastSaturationWarning < SaturationWarningInterval)
        {
            return;
        }

        _lastSaturationWarning = now;
        _logger.LogWarning(
            "RBAC cache '{CacheName}' is saturated at {Capacity} entries — serving this request " +
            "uncached rather than denying it. Raise Rbac:MaxCacheEntries if this persists.",
            _cacheName,
            _capacity);
    }

    private sealed class Entry
    {
        public TaskCompletionSource<TValue>? Pending;
        public TValue? Value;
        public bool HasValue;
        public DateTimeOffset ExpiresAt;
    }
}
