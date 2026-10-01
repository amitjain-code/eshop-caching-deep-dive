using System.Globalization;
using Catalog.Core.Application.Abstractions;
using EShop.Caching.Redis;
using StackExchange.Redis;

namespace Catalog.Infrastructure.Redis;

/// <summary>
/// HyperLogLog cardinality estimation.
/// <code>
/// PFADD   views:42:20261001 "visitor-abc"   O(1)
/// PFCOUNT views:42:20261001 views:42:20260930 ...   union of days, still 12 KB each
/// </code>
/// </summary>
internal sealed class RedisProductViewCounter(IRedisStore redis) : IProductViewCounter
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(95);

    public async Task RecordAsync(int productId, string visitorId, DateOnly day)
    {
        var key = Key(productId, day);
        var added = await redis.Database.HyperLogLogAddAsync(key, visitorId).ConfigureAwait(false);
        if (added)
        {
            await redis.Database.KeyExpireAsync(key, Retention, CommandFlags.FireAndForget).ConfigureAwait(false);
        }
    }

    public Task<long> CountUniqueAsync(int productId, DateOnly today, int days)
    {
        var keys = Enumerable.Range(0, days).Select(i => Key(productId, today.AddDays(-i))).ToArray();
        return redis.Database.HyperLogLogLengthAsync(keys);
    }

    private static RedisKey Key(int productId, DateOnly day) =>
        string.Create(CultureInfo.InvariantCulture, $"views:{productId}:{day:yyyyMMdd}");
}
