using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace EShop.Caching.Http;

/// <summary>
/// Minimal-API endpoint filter that turns an <see cref="HttpCacheProfile"/> into response headers:
/// Cache-Control, CDN-Cache-Control, Vary, ETag, Last-Modified, Cache-Tag / Surrogate-Key,
/// and short-circuits conditional requests with <c>304 Not Modified</c>.
/// </summary>
internal sealed class HttpCacheEndpointFilter(HttpCacheProfile profile) : IEndpointFilter
{
    public const string CacheTagHeader = "Cache-Tag";
    public const string SurrogateKeyHeader = "Surrogate-Key";
    public const string CdnCacheControlHeader = "CDN-Cache-Control";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context).ConfigureAwait(false);
        var http = context.HttpContext;

        if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
        {
            return result;
        }

        if (!TryGetSuccessValue(result, out var value))
        {
            // Errors must never be cached by shared caches for the success lifetime.
            http.Response.Headers.CacheControl = "no-store";
            return result;
        }

        var headers = http.Response.Headers;
        headers.CacheControl = profile.ToCacheControlHeaderValue();

        if (profile.ToCdnCacheControlHeaderValue() is { } cdn)
        {
            headers[CdnCacheControlHeader] = cdn;
        }

        if (profile.Vary.Count > 0)
        {
            headers.Vary = string.Join(", ", profile.Vary);
        }

        if (profile.Cacheability == HttpCacheability.Public && value is IHasCacheTags tagged)
        {
            var tags = tagged.CacheTags.Distinct(StringComparer.Ordinal).ToArray();
            if (tags.Length > 0)
            {
                headers[CacheTagHeader] = string.Join(',', tags);      // Cloudflare / gateway
                headers[SurrogateKeyHeader] = string.Join(' ', tags);  // Fastly / Varnish
            }
        }

        if (value is IHasLastModified modified)
        {
            headers.LastModified = modified.LastModified.ToUniversalTime().ToString("R");
        }

        if (!profile.EnableETag || profile.Cacheability == HttpCacheability.NoStore)
        {
            return result;
        }

        var etag = CreateETag(http, value);
        headers.ETag = etag.ToString();

        return IsNotModified(http.Request, etag)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : result;
    }

    internal static EntityTagHeaderValue CreateETag(HttpContext http, object value)
    {
        if (value is IVersionedResource versioned)
        {
            return new EntityTagHeaderValue($"\"{versioned.ResourceVersion}\"", isWeak: true);
        }

        // Fallback: hash of the JSON representation. Costs one extra serialization but needs no versioning.
        var jsonOptions = http.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions
            ?? JsonSerializerOptions.Web;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), jsonOptions);
        var hash = SHA256.HashData(bytes);
        return new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(hash.AsSpan(0, 12))}\"", isWeak: true);
    }

    internal static bool IsNotModified(HttpRequest request, EntityTagHeaderValue etag)
    {
        var ifNoneMatch = request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch is null || ifNoneMatch.Count == 0)
        {
            return false;
        }

        // RFC 9110: If-None-Match uses weak comparison.
        return ifNoneMatch.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(etag, useStrongComparison: false));
    }

    private static bool TryGetSuccessValue(object? result, out object value)
    {
        while (result is INestedHttpResult nested)
        {
            result = nested.Result;
        }

        switch (result)
        {
            case IStatusCodeHttpResult { StatusCode: StatusCodes.Status200OK } and IValueHttpResult { Value: { } v }:
                value = v;
                return true;
            case IResult or null:
                value = default!;
                return false;
            default:
                value = result; // handler returned a plain object => 200 OK + JSON
                return true;
        }
    }
}
