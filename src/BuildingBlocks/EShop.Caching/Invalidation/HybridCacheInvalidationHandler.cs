using Microsoft.Extensions.Caching.Hybrid;

namespace EShop.Caching.Invalidation;

/// <summary>
/// HybridCache invalidation is local + L2 only: other nodes keep serving their L1 copy until it expires.
/// Running this handler on every node (triggered via Pub/Sub) closes that gap.
/// </summary>
internal sealed class HybridCacheInvalidationHandler(HybridCache cache) : ICacheInvalidationHandler
{
    public async ValueTask HandleAsync(CacheInvalidation invalidation, CancellationToken cancellationToken)
    {
        if (invalidation.Tags.Count > 0)
        {
            await cache.RemoveByTagAsync(invalidation.Tags, cancellationToken).ConfigureAwait(false);
        }

        if (invalidation.Keys.Count > 0)
        {
            await cache.RemoveAsync(invalidation.Keys, cancellationToken).ConfigureAwait(false);
        }
    }
}
