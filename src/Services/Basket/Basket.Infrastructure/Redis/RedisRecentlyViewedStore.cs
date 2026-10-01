using Basket.Core.Application.Abstractions;
using Basket.Core.Application.Engagement;
using EShop.Caching.Redis;
using StackExchange.Redis;

namespace Basket.Infrastructure.Redis;

/// <summary>
/// "Recently viewed" as a capped Redis <b>List</b>:
/// <code>
/// MULTI
///   LREM  recent:{user} 0 42     drop older occurrence (de-dupe)
///   LPUSH recent:{user} 42       newest first, O(1)
///   LTRIM recent:{user} 0 19     cap at 20 -> bounded memory
///   EXPIRE recent:{user} 30d
/// EXEC
/// </code>
/// A Set loses ordering, a Sorted Set works but costs more memory; a List is the natural deque.
/// </summary>
internal sealed class RedisRecentlyViewedStore(IRedisStore redis) : IRecentlyViewedStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public async Task AddAsync(string userId, int productId)
    {
        var key = Key(userId);
        var transaction = redis.Database.CreateTransaction();
        _ = transaction.ListRemoveAsync(key, productId);
        _ = transaction.ListLeftPushAsync(key, productId);
        _ = transaction.ListTrimAsync(key, 0, RecentlyViewedHandlers.Capacity - 1);
        _ = transaction.KeyExpireAsync(key, Lifetime);
        await transaction.ExecuteAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<int>> GetAsync(string userId, int take)
    {
        var values = await redis.Database.ListRangeAsync(Key(userId), 0, take - 1).ConfigureAwait(false);
        return [.. values.Select(v => (int)v)];
    }

    private static RedisKey Key(string userId) => $"recent:{userId}";
}
