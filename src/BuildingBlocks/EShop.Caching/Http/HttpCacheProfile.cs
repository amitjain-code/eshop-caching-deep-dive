using System.Globalization;

namespace EShop.Caching.Http;

public enum HttpCacheability
{
    /// <summary>Nobody may store the response (secrets, one-time tokens, checkout).</summary>
    NoStore,

    /// <summary>Only the end user's browser may store it (user-specific data).</summary>
    Private,

    /// <summary>Browsers, CDNs and reverse proxies may store it.</summary>
    Public,
}

/// <summary>
/// Declarative description of how a response may be cached by the browser (max-age),
/// by shared caches such as reverse proxies (s-maxage) and by CDNs (CDN-Cache-Control, RFC 9213).
/// </summary>
public sealed record HttpCacheProfile
{
    public required string Name { get; init; }

    public HttpCacheability Cacheability { get; init; } = HttpCacheability.Public;

    /// <summary>Browser freshness lifetime (Cache-Control: max-age).</summary>
    public TimeSpan MaxAge { get; init; }

    /// <summary>Shared cache lifetime - reverse proxy / gateway (Cache-Control: s-maxage). Ignored for private.</summary>
    public TimeSpan? SharedMaxAge { get; init; }

    /// <summary>CDN-only lifetime (CDN-Cache-Control: max-age). Lets the edge keep content long and rely on purge.</summary>
    public TimeSpan? CdnMaxAge { get; init; }

    /// <summary>Serve stale while refreshing in the background (RFC 5861).</summary>
    public TimeSpan? StaleWhileRevalidate { get; init; }

    /// <summary>Serve stale when the origin is failing (RFC 5861) - resilience for free.</summary>
    public TimeSpan? StaleIfError { get; init; }

    /// <summary>Once stale, the cache must revalidate (conditional GET) before reuse.</summary>
    public bool MustRevalidate { get; init; }

    /// <summary>Content never changes for this URL (fingerprinted assets).</summary>
    public bool Immutable { get; init; }

    /// <summary>Request headers that select different representations (Vary).</summary>
    public IReadOnlyList<string> Vary { get; init; } = [];

    /// <summary>Emit an ETag and answer If-None-Match with 304 Not Modified.</summary>
    public bool EnableETag { get; init; } = true;

    public string ToCacheControlHeaderValue()
    {
        if (Cacheability == HttpCacheability.NoStore)
        {
            return "no-store";
        }

        var parts = new List<string>
        {
            Cacheability == HttpCacheability.Public ? "public" : "private",
            $"max-age={Seconds(MaxAge)}",
        };

        if (Cacheability == HttpCacheability.Public && SharedMaxAge is { } shared)
        {
            parts.Add($"s-maxage={Seconds(shared)}");
        }

        if (StaleWhileRevalidate is { } swr)
        {
            parts.Add($"stale-while-revalidate={Seconds(swr)}");
        }

        if (StaleIfError is { } sie)
        {
            parts.Add($"stale-if-error={Seconds(sie)}");
        }

        if (MustRevalidate)
        {
            parts.Add("must-revalidate");
        }

        if (Immutable)
        {
            parts.Add("immutable");
        }

        return string.Join(", ", parts);
    }

    public string? ToCdnCacheControlHeaderValue() =>
        Cacheability == HttpCacheability.Public && CdnMaxAge is { } cdn ? $"max-age={Seconds(cdn)}" : null;

    private static string Seconds(TimeSpan value) =>
        ((long)Math.Max(0, value.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
}
