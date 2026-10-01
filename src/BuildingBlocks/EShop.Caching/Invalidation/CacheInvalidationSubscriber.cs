using System.Text.Json;
using EShop.Caching.Configuration;
using EShop.Caching.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EShop.Caching.Invalidation;

/// <summary>
/// Redis Pub/Sub listener: fire-and-forget fan-out to every node that is connected right now.
/// A node that is offline misses messages - acceptable here because its L1 is empty after restart
/// and L1 lifetimes are short. Use Streams when delivery must be guaranteed.
/// </summary>
internal sealed partial class CacheInvalidationSubscriber(
    IConnectionMultiplexer multiplexer,
    IEnumerable<ICacheInvalidationHandler> handlers,
    CacheNodeIdentity node,
    CacheMetrics metrics,
    IOptions<CachingOptions> options,
    ILogger<CacheInvalidationSubscriber> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = RedisChannel.Literal(options.Value.InvalidationChannel);
        var queue = await multiplexer.GetSubscriber().SubscribeAsync(channel).ConfigureAwait(false);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = await queue.ReadAsync(stoppingToken).ConfigureAwait(false);
                await HandleAsync(message.Message, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
        finally
        {
            await queue.UnsubscribeAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(RedisValue payload, CancellationToken cancellationToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<CacheInvalidationMessage>(payload.ToString());
            if (message is null || message.Origin == node.Id)
            {
                return;
            }

            var invalidation = new CacheInvalidation(message.Tags, message.Keys);
            foreach (var handler in handlers)
            {
                await handler.HandleAsync(invalidation, cancellationToken).ConfigureAwait(false);
            }

            metrics.RecordInvalidation("remote", message.Tags.Length, message.Keys.Length);
        }
        catch (JsonException ex)
        {
            LogMalformed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring malformed cache invalidation message")]
    private static partial void LogMalformed(ILogger logger, Exception exception);
}
