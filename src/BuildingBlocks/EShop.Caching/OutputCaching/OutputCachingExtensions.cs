using EShop.Caching.Configuration;
using EShop.Caching.Invalidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EShop.Caching.OutputCaching;

public static class OutputCachePolicyNames
{
    /// <summary>Policy for public API responses proxied by the gateway.</summary>
    public const string PublicApi = "PublicApi";
}

public static class OutputCachingExtensions
{
    /// <summary>
    /// Reverse-proxy cache backed by Redis so all gateway replicas share one cache and one eviction.
    /// Call after <see cref="DependencyInjection.CachingServiceCollectionExtensions.AddEShopCaching"/>.
    /// </summary>
    public static IHostApplicationBuilder AddEShopOutputCache(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = builder.Configuration.GetSection(CachingOptions.SectionName).Get<CachingOptions>() ?? new CachingOptions();
        var redis = builder.Configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("ConnectionStrings:redis is required for the output cache.");

        builder.Services.AddOutputCache(o =>
        {
            o.MaximumBodySize = 1024 * 1024;
            o.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(60);
            o.AddPolicy(OutputCachePolicyNames.PublicApi, OriginControlledOutputCachePolicy.Instance);
        });

        builder.Services.AddStackExchangeRedisOutputCache(o =>
        {
            o.Configuration = redis;
            o.InstanceName = $"{options.ServiceKeyPrefix}output:";
        });

        builder.Services.AddSingleton<ICacheInvalidationHandler, OutputCacheInvalidationHandler>();
        return builder;
    }
}
