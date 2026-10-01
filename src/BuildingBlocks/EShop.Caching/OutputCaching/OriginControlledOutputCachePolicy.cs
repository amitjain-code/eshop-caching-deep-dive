using EShop.Caching.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace EShop.Caching.OutputCaching;

/// <summary>
/// Reverse-proxy output cache policy that behaves like a well-mannered shared cache:
/// the <b>origin</b> decides cacheability through standard headers, the gateway just obeys.
/// <list type="bullet">
/// <item>only anonymous GET/HEAD requests are looked up / stored</item>
/// <item>only 200 responses with <c>Cache-Control: public</c> and <c>s-maxage</c>/<c>max-age</c> are stored</item>
/// <item>lifetime = s-maxage (falls back to max-age)</item>
/// <item>entries are tagged with the origin's <c>Cache-Tag</c> header so a product update can evict them</item>
/// </list>
/// </summary>
public sealed class OriginControlledOutputCachePolicy : IOutputCachePolicy
{
    public static OriginControlledOutputCachePolicy Instance { get; } = new();

    private OriginControlledOutputCachePolicy()
    {
    }

    ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var request = context.HttpContext.Request;
        var cacheable = (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && !request.Headers.ContainsKey(HeaderNames.Authorization)
            && !request.Headers.ContainsKey("X-User-Id")
            && !request.Headers.ContainsKey(HeaderNames.Cookie);

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true; // request coalescing: one origin call per key under load
        context.CacheVaryByRules.QueryKeys = "*";
        context.CacheVaryByRules.HeaderNames = new StringValues([HeaderNames.AcceptEncoding, HeaderNames.Accept]);
        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
        ValueTask.CompletedTask;

    ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var response = context.HttpContext.Response;

        if (response.StatusCode != StatusCodes.Status200OK || response.Headers.ContainsKey(HeaderNames.SetCookie))
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        if (!CacheControlHeaderValue.TryParse(response.Headers.CacheControl.ToString(), out var cacheControl)
            || !cacheControl.Public
            || cacheControl.Private
            || cacheControl.NoStore
            || cacheControl.NoCache)
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        var lifetime = cacheControl.SharedMaxAge ?? cacheControl.MaxAge;
        if (lifetime is null || lifetime <= TimeSpan.Zero)
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        context.ResponseExpirationTimeSpan = lifetime;

        foreach (var tag in response.Headers[HttpCacheEndpointFilter.CacheTagHeader].ToString()
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            context.Tags.Add(tag);
        }

        return ValueTask.CompletedTask;
    }
}
