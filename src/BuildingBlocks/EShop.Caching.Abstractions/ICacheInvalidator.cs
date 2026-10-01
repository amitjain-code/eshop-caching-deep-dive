namespace EShop.Caching;

/// <summary>What to invalidate after a write.</summary>
public sealed record CacheInvalidation(IReadOnlyCollection<string> Tags, IReadOnlyCollection<string> Keys)
{
    public static CacheInvalidation ForTags(params string[] tags) => new(tags, []);
}

/// <summary>
/// Invalidates every cache layer the platform controls: local L1 on every node (via Redis Pub/Sub),
/// the Redis L2, the gateway output cache and the CDN (purge by surrogate key / cache tag).
/// </summary>
public interface ICacheInvalidator
{
    Task InvalidateAsync(CacheInvalidation invalidation, CancellationToken cancellationToken = default);
}
