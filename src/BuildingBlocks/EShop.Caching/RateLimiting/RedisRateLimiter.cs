using EShop.Caching.Redis;
using StackExchange.Redis;

namespace EShop.Caching.RateLimiting;

public enum RateLimitAlgorithm
{
    /// <summary>Redis String + INCR/EXPIRE. O(1) memory per client; allows 2x burst at window edges.</summary>
    FixedWindow,

    /// <summary>Redis Sorted Set of request timestamps. Exact sliding window; O(limit) memory per client.</summary>
    SlidingWindow,
}

public readonly record struct RateLimitDecision(bool IsAllowed, long Limit, long Remaining, TimeSpan RetryAfter);

/// <summary>Distributed rate limiter: every API replica shares the same counters in Redis.</summary>
public sealed class RedisRateLimiter(IRedisStore redis, TimeProvider clock)
{
    // INCR and EXPIRE must be atomic, otherwise a crash between them leaves a counter that never expires.
    private const string FixedWindowScript = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
          redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return { current, redis.call('PTTL', KEYS[1]) }
        """;

    // Trim timestamps that left the window, count, admit if below limit.
    private const string SlidingWindowScript = """
        local key    = KEYS[1]
        local now    = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local limit  = tonumber(ARGV[3])
        redis.call('ZREMRANGEBYSCORE', key, '-inf', now - window)
        local count = redis.call('ZCARD', key)
        if count < limit then
          redis.call('ZADD', key, now, ARGV[4])
          redis.call('PEXPIRE', key, window)
          return { 1, limit - count - 1, 0 }
        end
        local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
        return { 0, 0, (tonumber(oldest[2]) + window) - now }
        """;

    public Task<RateLimitDecision> TryAcquireAsync(string policy, string partition, int limit, TimeSpan window, RateLimitAlgorithm algorithm) =>
        algorithm == RateLimitAlgorithm.FixedWindow
            ? FixedWindowAsync(policy, partition, limit, window)
            : SlidingWindowAsync(policy, partition, limit, window);

    private async Task<RateLimitDecision> FixedWindowAsync(string policy, string partition, int limit, TimeSpan window)
    {
        var windowMs = (long)window.TotalMilliseconds;
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var bucket = now / windowMs;
        RedisKey key = $"ratelimit:fixed:{policy}:{partition}:{bucket}";

        var raw = await redis.Database.ScriptEvaluateAsync(FixedWindowScript, [key], [windowMs]).ConfigureAwait(false);
        var result = (RedisResult[]?)raw ?? [];
        var count = (long)result[0];
        var ttl = Math.Max(0, (long)result[1]);

        return count <= limit
            ? new RateLimitDecision(true, limit, limit - count, TimeSpan.Zero)
            : new RateLimitDecision(false, limit, 0, TimeSpan.FromMilliseconds(ttl));
    }

    private async Task<RateLimitDecision> SlidingWindowAsync(string policy, string partition, int limit, TimeSpan window)
    {
        var windowMs = (long)window.TotalMilliseconds;
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        RedisKey key = $"ratelimit:sliding:{policy}:{partition}";
        var member = $"{now}:{Guid.NewGuid():N}";

        var raw = await redis.Database.ScriptEvaluateAsync(SlidingWindowScript, [key], [now, windowMs, limit, member]).ConfigureAwait(false);
        var result = (RedisResult[]?)raw ?? [];

        var allowed = (long)result[0] == 1;
        return new RateLimitDecision(allowed, limit, (long)result[1], TimeSpan.FromMilliseconds(Math.Max(0, (long)result[2])));
    }
}
