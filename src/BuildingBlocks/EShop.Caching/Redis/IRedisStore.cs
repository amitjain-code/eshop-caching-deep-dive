using EShop.Caching.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace EShop.Caching.Redis;

/// <summary>Access to Redis for data-structure work that goes beyond key/value caching.</summary>
public interface IRedisStore
{
    /// <summary>Database whose keys are automatically prefixed with <c>{prefix}:{service}:</c> (service-owned data).</summary>
    IDatabase Database { get; }

    /// <summary>Un-prefixed database for keys shared between services (e.g. integration streams).</summary>
    IDatabase SharedDatabase { get; }

    ISubscriber Subscriber { get; }
}

internal sealed class RedisStore(IConnectionMultiplexer multiplexer, IOptions<CachingOptions> options) : IRedisStore
{
    public IDatabase Database { get; } = multiplexer.GetDatabase().WithKeyPrefix(options.Value.ServiceKeyPrefix);

    public IDatabase SharedDatabase => multiplexer.GetDatabase();

    public ISubscriber Subscriber => multiplexer.GetSubscriber();
}
