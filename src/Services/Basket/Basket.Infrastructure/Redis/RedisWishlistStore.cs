using Basket.Core.Application.Abstractions;
using EShop.Caching.Redis;
using StackExchange.Redis;

namespace Basket.Infrastructure.Redis;

/// <summary>
/// Wishlist as a Redis <b>Set</b>: duplicates are impossible by construction, SISMEMBER is O(1)
/// (render the heart icon on every product tile), and SINTER answers "what do we both want?".
/// </summary>
internal sealed class RedisWishlistStore(IRedisStore redis) : IWishlistStore
{
    public Task<bool> AddAsync(string userId, int productId) => redis.Database.SetAddAsync(Key(userId), productId);

    public Task<bool> RemoveAsync(string userId, int productId) => redis.Database.SetRemoveAsync(Key(userId), productId);

    public Task<bool> ContainsAsync(string userId, int productId) => redis.Database.SetContainsAsync(Key(userId), productId);

    public async Task<IReadOnlyList<int>> GetAsync(string userId)
    {
        var members = await redis.Database.SetMembersAsync(Key(userId)).ConfigureAwait(false);
        return [.. members.Select(m => (int)m).Order()];
    }

    public async Task<IReadOnlyList<int>> CommonAsync(string userId, string otherUserId)
    {
        var members = await redis.Database.SetCombineAsync(SetOperation.Intersect, Key(userId), Key(otherUserId)).ConfigureAwait(false);
        return [.. members.Select(m => (int)m).Order()];
    }

    private static RedisKey Key(string userId) => $"wishlist:{userId}";
}
