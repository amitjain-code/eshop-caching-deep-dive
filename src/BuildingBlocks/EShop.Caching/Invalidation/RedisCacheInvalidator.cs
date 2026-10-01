using System.Text.Json;
using EShop.Caching.Cdn;
using EShop.Caching.Configuration;
using EShop.Caching.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EShop.Caching.Invalidation;

/// <summary>
/// Write-path invalidation across all layers:
/// 1. local handlers (L1 + L2 on this node, output cache if hosted here)
/// 2. Redis Pub/Sub broadcast so every other node drops its L1 copy
/// 3. CDN purge by cache tag (surrogate key)
/// </summary>
internal sealed partial class RedisCacheInvalidator(
    IConnectionMultiplexer multiplexer,
    IEnumerable<ICacheInvalidationHandler> handlers,
    ICdnPurger cdnPurger,
    CacheNodeIdentity node,
    CacheMetrics metrics,
    IOptions<CachingOptions> options,
    ILogger<RedisCacheInvalidator> logger) : ICacheInvalidator
{
    public async Task InvalidateAsync(CacheInvalidation invalidation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invalidation);

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(invalidation, cancellationToken).ConfigureAwait(false);
        }

        metrics.RecordInvalidation("local", invalidation.Tags.Count, invalidation.Keys.Count);

        var message = new CacheInvalidationMessage(node.Id, [.. invalidation.Tags], [.. invalidation.Keys]);
        var receivers = await multiplexer.GetSubscriber()
            .PublishAsync(RedisChannel.Literal(options.Value.InvalidationChannel), JsonSerializer.Serialize(message))
            .ConfigureAwait(false);

        LogPublished(logger, invalidation.Tags.Count, invalidation.Keys.Count, receivers);

        if (invalidation.Tags.Count > 0)
        {
            await cdnPurger.PurgeTagsAsync(invalidation.Tags, cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cache invalidation published: {TagCount} tags, {KeyCount} keys, {Receivers} subscribers")]
    private static partial void LogPublished(ILogger logger, int tagCount, int keyCount, long receivers);
}
