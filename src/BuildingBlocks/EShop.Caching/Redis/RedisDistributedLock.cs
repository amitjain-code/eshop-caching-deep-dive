using StackExchange.Redis;

namespace EShop.Caching.Redis;

/// <summary>
/// Redis String used as a lease: <c>SET lock:{resource} {token} NX PX {lease}</c>.
/// Release is a compare-and-delete so a node can never release a lock another node re-acquired after expiry.
/// Good for efficiency locks (avoid duplicate work). For correctness-critical locks prefer fencing tokens.
/// </summary>
internal sealed class RedisDistributedLock(IRedisStore redis) : IDistributedLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(string resource, TimeSpan lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        var key = (RedisKey)$"lock:{resource}";
        var token = (RedisValue)Guid.NewGuid().ToString("N");

        var acquired = await redis.Database.LockTakeAsync(key, token, lease).ConfigureAwait(false);
        return acquired ? new Lease(redis.Database, key, token) : null;
    }

    private sealed class Lease(IDatabase database, RedisKey key, RedisValue token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await database.LockReleaseAsync(key, token).ConfigureAwait(false);
    }
}
