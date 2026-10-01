using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace EShop.Caching.Http;

public static class HttpCachingEndpointExtensions
{
    /// <summary>Applies browser/CDN/proxy caching headers and conditional-GET handling to an endpoint or group.</summary>
    public static TBuilder CacheHttp<TBuilder>(this TBuilder builder, HttpCacheProfile profile)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(profile);
        return builder.AddEndpointFilter(new HttpCacheEndpointFilter(profile));
    }
}
