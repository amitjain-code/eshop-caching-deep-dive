using System.Globalization;
using Basket.Core.Application.Abstractions;
using EShop.Caching.Redis;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Basket.Infrastructure.Redis;

/// <summary>
/// Daily active users with <b>Bitmaps</b>: bit N of <c>dau:{yyyyMMdd}</c> = user #N was active.
/// 10 million users = 10M bits = 1.25 MB per day, versus ~500 MB for a Set of string ids.
/// <code>
/// SETBIT   dau:20261001 1234 1
/// BITCOUNT dau:20261001                               -> DAU
/// BITOP OR dau:7d dau:20261001 ... dau:20260925; BITCOUNT dau:7d   -> WAU
/// </code>
/// Bitmaps need dense integer ids, so string user ids are mapped once via HSETNX/INCR (String counter)
/// and the mapping is kept in the local memory cache (it never changes: perfect L1 data).
/// </summary>
internal sealed class RedisActiveUserTracker(IRedisStore redis, IMemoryCache localCache) : IActiveUserTracker
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(40);

    // {users} hash tag keeps both keys in one Redis Cluster slot so the script stays valid when sharded.
    private const string GetOrCreateIdScript = """
        local id = redis.call('HGET', KEYS[1], ARGV[1])
        if id then return tonumber(id) end
        id = redis.call('INCR', KEYS[2])
        redis.call('HSET', KEYS[1], ARGV[1], id)
        return id
        """;

    public async Task TrackAsync(string userId, DateOnly day)
    {
        var offset = await GetNumericIdAsync(userId).ConfigureAwait(false);
        var key = DayKey(day);
        var batch = redis.Database.CreateBatch();
        var set = batch.StringSetBitAsync(key, offset, true);
        var expire = batch.KeyExpireAsync(key, Retention);
        batch.Execute();
        await Task.WhenAll(set, expire).ConfigureAwait(false);
    }

    public async Task<long> CountAsync(DateOnly endDay, int days)
    {
        if (days == 1)
        {
            return await redis.Database.StringBitCountAsync(DayKey(endDay)).ConfigureAwait(false);
        }

        var keys = Enumerable.Range(0, days).Select(i => DayKey(endDay.AddDays(-i))).ToArray();
        RedisKey destination = $"active:{endDay:yyyyMMdd}:{days}d";
        await redis.Database.StringBitOperationAsync(Bitwise.Or, destination, keys).ConfigureAwait(false);
        await redis.Database.KeyExpireAsync(destination, TimeSpan.FromMinutes(5)).ConfigureAwait(false);
        return await redis.Database.StringBitCountAsync(destination).ConfigureAwait(false);
    }

    public async Task<bool> WasActiveAsync(string userId, DateOnly day)
    {
        var offset = await GetNumericIdAsync(userId).ConfigureAwait(false);
        return await redis.Database.StringGetBitAsync(DayKey(day), offset).ConfigureAwait(false);
    }

    private async Task<long> GetNumericIdAsync(string userId)
    {
        if (localCache.TryGetValue<long>($"uid:{userId}", out var cached))
        {
            return cached;
        }

        var result = await redis.Database.ScriptEvaluateAsync(
            GetOrCreateIdScript, ["{users}:ids", "{users}:seq"], [userId]).ConfigureAwait(false);
        var id = (long)result;
        localCache.Set($"uid:{userId}", id, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(1) });
        return id;
    }

    private static RedisKey DayKey(DateOnly day) =>
        string.Create(CultureInfo.InvariantCulture, $"dau:{day:yyyyMMdd}");
}
