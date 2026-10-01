using EShop.Caching.Invalidation;
using Microsoft.AspNetCore.OutputCaching;

namespace EShop.Caching.OutputCaching;

/// <summary>Evicts gateway output-cache entries whose tags match an invalidation broadcast.</summary>
internal sealed class OutputCacheInvalidationHandler(IOutputCacheStore store) : ICacheInvalidationHandler
{
    public async ValueTask HandleAsync(CacheInvalidation invalidation, CancellationToken cancellationToken)
    {
        foreach (var tag in invalidation.Tags)
        {
            await store.EvictByTagAsync(tag, cancellationToken).ConfigureAwait(false);
        }
    }
}
