using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace EShop.Caching.Redis;

internal sealed class RedisHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await multiplexer.GetDatabase().PingAsync().ConfigureAwait(false);
            return latency > TimeSpan.FromMilliseconds(250)
                ? HealthCheckResult.Degraded($"Redis latency {latency.TotalMilliseconds:F1} ms")
                : HealthCheckResult.Healthy($"Redis latency {latency.TotalMilliseconds:F1} ms");
        }
        catch (RedisException ex)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable", ex);
        }
    }
}
