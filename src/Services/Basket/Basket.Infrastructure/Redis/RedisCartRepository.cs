using System.Globalization;
using System.Text.Json;
using Basket.Core.Application.Abstractions;
using Basket.Core.Domain;
using EShop.Caching.Redis;
using StackExchange.Redis;

namespace Basket.Infrastructure.Redis;

/// <summary>
/// Cart as a Redis <b>Hash</b>:
/// <code>
/// cart:{buyer}  qty:42  -> 2                 (HINCRBY: atomic, no read-modify-write race)
///               item:42 -> {"name":..,"price":..}   (snapshot)
/// </code>
/// Storing the cart as one JSON String would force GET + modify + SET for every click, and two tabs
/// adding items concurrently would overwrite each other (lost update). Hash fields change independently.
/// </summary>
internal sealed class RedisCartRepository(IRedisStore redis) : ICartRepository
{
    private static readonly TimeSpan CartLifetime = TimeSpan.FromDays(30);
    private static readonly long CartLifetimeSeconds = (long)CartLifetime.TotalSeconds;

    // Increment, clean up when it drops to zero, refresh snapshot + TTL - all atomically.
    private const string AddOrIncrementScript = """
        local qty = redis.call('HINCRBY', KEYS[1], 'qty:' .. ARGV[1], ARGV[2])
        if qty <= 0 then
          redis.call('HDEL', KEYS[1], 'qty:' .. ARGV[1], 'item:' .. ARGV[1])
        else
          redis.call('HSET', KEYS[1], 'item:' .. ARGV[1], ARGV[3])
        end
        redis.call('EXPIRE', KEYS[1], ARGV[4])
        return qty
        """;

    private const string SetQuantityScript = """
        if redis.call('HEXISTS', KEYS[1], 'item:' .. ARGV[1]) == 0 then
          return 0
        end
        if tonumber(ARGV[2]) <= 0 then
          redis.call('HDEL', KEYS[1], 'qty:' .. ARGV[1], 'item:' .. ARGV[1])
        else
          redis.call('HSET', KEYS[1], 'qty:' .. ARGV[1], ARGV[2])
        end
        redis.call('EXPIRE', KEYS[1], ARGV[3])
        return 1
        """;

    public async Task<ShoppingCart> GetAsync(string buyerId)
    {
        var fields = await redis.Database.HashGetAllAsync(Key(buyerId)).ConfigureAwait(false);
        if (fields.Length == 0)
        {
            return ShoppingCart.Empty(buyerId);
        }

        var quantities = new Dictionary<int, int>();
        var snapshots = new Dictionary<int, ItemSnapshot>();
        foreach (var field in fields)
        {
            var name = field.Name.ToString();
            if (name.StartsWith("qty:", StringComparison.Ordinal))
            {
                quantities[ParseId(name)] = (int)field.Value;
            }
            else if (name.StartsWith("item:", StringComparison.Ordinal))
            {
                var snapshot = JsonSerializer.Deserialize<ItemSnapshot>(field.Value.ToString());
                if (snapshot is not null)
                {
                    snapshots[ParseId(name)] = snapshot;
                }
            }
        }

        var items = snapshots
            .Where(s => quantities.ContainsKey(s.Key))
            .Select(s => new CartItem(s.Key, s.Value.Name, s.Value.Price, quantities[s.Key], s.Value.Picture));

        return new ShoppingCart(buyerId, items);
    }

    public async Task<long> AddOrIncrementAsync(string buyerId, CartItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var snapshot = JsonSerializer.Serialize(new ItemSnapshot(item.ProductName, item.UnitPrice, item.PictureUrl));
        var result = await redis.Database.ScriptEvaluateAsync(
            AddOrIncrementScript,
            [Key(buyerId)],
            [item.ProductId, item.Quantity, snapshot, CartLifetimeSeconds]).ConfigureAwait(false);
        return (long)result;
    }

    public async Task<bool> SetQuantityAsync(string buyerId, int productId, int quantity)
    {
        var result = await redis.Database.ScriptEvaluateAsync(
            SetQuantityScript,
            [Key(buyerId)],
            [productId, quantity, CartLifetimeSeconds]).ConfigureAwait(false);
        return (long)result == 1;
    }

    public async Task<bool> RemoveAsync(string buyerId, int productId)
    {
        var id = productId.ToString(CultureInfo.InvariantCulture);
        var removed = await redis.Database.HashDeleteAsync(Key(buyerId), new RedisValue[] { $"qty:{id}", $"item:{id}" }).ConfigureAwait(false);
        return removed > 0;
    }

    public Task DeleteAsync(string buyerId) => redis.Database.KeyDeleteAsync(Key(buyerId));

    private static RedisKey Key(string buyerId) => $"cart:{buyerId}";

    private static int ParseId(string field) =>
        int.Parse(field.AsSpan(field.IndexOf(':', StringComparison.Ordinal) + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private sealed record ItemSnapshot(string Name, decimal Price, string? Picture);
}
