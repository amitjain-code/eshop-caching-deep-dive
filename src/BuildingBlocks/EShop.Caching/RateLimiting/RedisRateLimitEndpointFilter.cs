using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.Caching.RateLimiting;

internal sealed class RedisRateLimitEndpointFilter(string policy, int limit, TimeSpan window, RateLimitAlgorithm algorithm) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var limiter = http.RequestServices.GetRequiredService<RedisRateLimiter>();
        var partition = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        var decision = await limiter.TryAcquireAsync(policy, partition, limit, window, algorithm).ConfigureAwait(false);
        http.Response.Headers["X-RateLimit-Limit"] = decision.Limit.ToString(CultureInfo.InvariantCulture);
        http.Response.Headers["X-RateLimit-Remaining"] = decision.Remaining.ToString(CultureInfo.InvariantCulture);

        if (decision.IsAllowed)
        {
            return await next(context).ConfigureAwait(false);
        }

        http.Response.Headers.RetryAfter = Math.Ceiling(decision.RetryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        return TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests",
            detail: $"Rate limit '{policy}' allows {limit} requests per {window.TotalSeconds:F0}s.");
    }
}

public static class RedisRateLimitEndpointExtensions
{
    public static TBuilder RequireRedisRateLimit<TBuilder>(
        this TBuilder builder,
        string policy,
        int limit,
        TimeSpan window,
        RateLimitAlgorithm algorithm = RateLimitAlgorithm.SlidingWindow)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return builder.AddEndpointFilter(new RedisRateLimitEndpointFilter(policy, limit, window, algorithm));
    }
}
