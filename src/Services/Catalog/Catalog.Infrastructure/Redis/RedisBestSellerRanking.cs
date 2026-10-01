using System.Globalization;
using Catalog.Core.Application.Abstractions;
using EShop.Caching.Redis;
using StackExchange.Redis;

namespace Catalog.Infrastructure.Redis;

/// <summary>
/// Leaderboard with Sorted Sets.
/// <code>
/// ZINCRBY bestsellers:20261001 3 "42"           O(log N)  - record 3 units of product 42
/// ZUNIONSTORE bestsellers:last7d 7 d1..d7       O(N log N) - roll-up, cached 60 s
/// ZRANGE bestsellers:last7d 0 9 REV WITHSCORES  O(log N + M) - top 10
/// </code>
/// A relational "ORDER BY SUM(qty)" over order lines gets slower as orders grow; the sorted set is
/// always ordered, so reading the top N is constant work regardless of history size.
/// </summary>
internal sealed class RedisBestSellerRanking(IRedisStore redis) : IBestSellerRanking
{
    private static readonly TimeSpan DailyRetention = TimeSpan.FromDays(35);
    private static readonly TimeSpan RollupLifetime = TimeSpan.FromSeconds(60);

    public async Task IncrementAsync(DateOnly day, IEnumerable<(int ProductId, int Quantity)> lines)
    {
        var key = DailyKey(day);
        var batch = redis.Database.CreateBatch();
        var tasks = lines
            .Where(l => l.Quantity > 0)
            .Select(l => (Task)batch.SortedSetIncrementAsync(key, l.ProductId, l.Quantity))
            .Append(batch.KeyExpireAsync(key, DailyRetention))
            .ToList();
        batch.Execute(); // pipelined: one round trip for all lines
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(int ProductId, long UnitsSold)>> TopAsync(DateOnly today, int days, int count)
    {
        var db = redis.Database;
        RedisKey source;

        if (days == 1)
        {
            source = DailyKey(today);
        }
        else
        {
            source = $"bestsellers:last{days}d";
            if (!await db.KeyExistsAsync(source).ConfigureAwait(false))
            {
                var keys = Enumerable.Range(0, days).Select(i => DailyKey(today.AddDays(-i))).ToArray();
                await db.SortedSetCombineAndStoreAsync(SetOperation.Union, source, keys, weights: null, Aggregate.Sum).ConfigureAwait(false);
                await db.KeyExpireAsync(source, RollupLifetime).ConfigureAwait(false);
            }
        }

        var top = await db.SortedSetRangeByRankWithScoresAsync(source, 0, count - 1, Order.Descending).ConfigureAwait(false);
        return [.. top.Select(e => ((int)e.Element, (long)e.Score))];
    }

    private static RedisKey DailyKey(DateOnly day) =>
        $"bestsellers:{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
}
