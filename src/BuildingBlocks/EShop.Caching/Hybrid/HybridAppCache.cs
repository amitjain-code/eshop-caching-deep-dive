using EShop.Caching.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;

namespace EShop.Caching.Hybrid;

/// <summary>
/// <see cref="IAppCache"/> on top of <see cref="HybridCache"/>:
/// L1 = in-process MemoryCache (nanoseconds, per node), L2 = Redis String (sub-millisecond, shared).
/// Concurrent misses for the same key run the factory once per node (stampede protection).
/// </summary>
internal sealed class HybridAppCache(HybridCache cache, CacheMetrics metrics) : IAppCache
{
    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CachePolicy policy,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        var (expiration, localExpiration) = policy.WithJitter();
        var options = new HybridCacheEntryOptions
        {
            Expiration = expiration,
            LocalCacheExpiration = localExpiration,
        };

        var factoryRan = false;
        var value = await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                factoryRan = true;
                return await factory(token).ConfigureAwait(false);
            },
            options,
            tags,
            cancellationToken).ConfigureAwait(false);

        metrics.RecordLookup(CacheName(key), hit: !factoryRan);
        return value;
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);

    // "catalog:product:42" -> "catalog:product" keeps metric cardinality bounded.
    private static string CacheName(string key)
    {
        var first = key.IndexOf(':', StringComparison.Ordinal);
        if (first < 0)
        {
            return key;
        }

        var second = key.IndexOf(':', first + 1);
        return second < 0 ? key : key[..second];
    }
}
