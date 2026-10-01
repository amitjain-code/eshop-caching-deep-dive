namespace EShop.Caching.Http;

/// <summary>Opinionated, reusable HTTP caching profiles used across the eShop APIs.</summary>
public static class HttpCacheProfiles
{
    /// <summary>Menus, categories, brands. Browser 5 min, gateway 1 h, CDN 1 day (purged on change).</summary>
    public static HttpCacheProfile PublicReferenceData { get; } = new()
    {
        Name = nameof(PublicReferenceData),
        Cacheability = HttpCacheability.Public,
        MaxAge = TimeSpan.FromMinutes(5),
        SharedMaxAge = TimeSpan.FromHours(1),
        CdnMaxAge = TimeSpan.FromDays(1),
        StaleWhileRevalidate = TimeSpan.FromMinutes(10),
        StaleIfError = TimeSpan.FromDays(1),
        Vary = ["Accept-Encoding"],
    };

    /// <summary>Product detail and listings. Browser 1 min, gateway 5 min, CDN 10 min.</summary>
    public static HttpCacheProfile PublicCatalog { get; } = new()
    {
        Name = nameof(PublicCatalog),
        Cacheability = HttpCacheability.Public,
        MaxAge = TimeSpan.FromMinutes(1),
        SharedMaxAge = TimeSpan.FromMinutes(5),
        CdnMaxAge = TimeSpan.FromMinutes(10),
        StaleWhileRevalidate = TimeSpan.FromMinutes(1),
        StaleIfError = TimeSpan.FromHours(1),
        Vary = ["Accept-Encoding"],
    };

    /// <summary>Fast-moving public data: best-sellers, suggestions, store search. 30 s / 60 s.</summary>
    public static HttpCacheProfile PublicShortLived { get; } = new()
    {
        Name = nameof(PublicShortLived),
        Cacheability = HttpCacheability.Public,
        MaxAge = TimeSpan.FromSeconds(30),
        SharedMaxAge = TimeSpan.FromSeconds(60),
        StaleWhileRevalidate = TimeSpan.FromSeconds(30),
        Vary = ["Accept-Encoding"],
        EnableETag = false,
    };

    /// <summary>
    /// User-specific data that must always be fresh (basket): the browser stores it but revalidates every
    /// time with If-None-Match; an unchanged basket costs a 304 with no body.
    /// </summary>
    public static HttpCacheProfile PrivateRevalidate { get; } = new()
    {
        Name = nameof(PrivateRevalidate),
        Cacheability = HttpCacheability.Private,
        MaxAge = TimeSpan.Zero,
        MustRevalidate = true,
        Vary = ["X-User-Id", "Authorization"],
    };

    /// <summary>User-specific data where a few seconds of staleness is fine (recently viewed, wishlist).</summary>
    public static HttpCacheProfile PrivateShortLived { get; } = new()
    {
        Name = nameof(PrivateShortLived),
        Cacheability = HttpCacheability.Private,
        MaxAge = TimeSpan.FromSeconds(30),
        Vary = ["X-User-Id", "Authorization"],
    };

    /// <summary>Sessions, checkout, anything with secrets.</summary>
    public static HttpCacheProfile NoStore { get; } = new()
    {
        Name = nameof(NoStore),
        Cacheability = HttpCacheability.NoStore,
        EnableETag = false,
    };
}
