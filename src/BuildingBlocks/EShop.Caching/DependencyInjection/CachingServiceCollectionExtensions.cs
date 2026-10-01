using EShop.Caching.Cdn;
using EShop.Caching.Configuration;
using EShop.Caching.Diagnostics;
using EShop.Caching.Hybrid;
using EShop.Caching.Invalidation;
using EShop.Caching.RateLimiting;
using EShop.Caching.Redis;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace EShop.Caching.DependencyInjection;

public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the caching platform:
    /// Redis connection, HybridCache (L1 memory + L2 Redis), IDistributedCache (sessions),
    /// cross-node invalidation over Pub/Sub, CDN purger, distributed lock, rate limiter, metrics, health check.
    /// </summary>
    public static IHostApplicationBuilder AddEShopCaching(this IHostApplicationBuilder builder, Action<CachingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var section = builder.Configuration.GetSection(CachingOptions.SectionName);
        builder.Services.AddOptions<CachingOptions>()
            .Bind(section)
            .Configure(o => configure?.Invoke(o))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var options = section.Get<CachingOptions>() ?? new CachingOptions();
        configure?.Invoke(options);

        var redisConnectionString = builder.Configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("ConnectionStrings:redis is required.");

        // One multiplexer per process: it is thread-safe and pipelines commands over a single socket.
        builder.Services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            configuration.AbortOnConnectFail = false; // keep retrying instead of crashing at startup
            configuration.ClientName = options.ServiceName;
            return ConnectionMultiplexer.Connect(configuration);
        });

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<CacheNodeIdentity>();
        builder.Services.TryAddSingleton<CacheMetrics>();
        builder.Services.TryAddSingleton<IRedisStore, RedisStore>();
        builder.Services.TryAddSingleton<IDistributedLock, RedisDistributedLock>();
        builder.Services.TryAddSingleton<RedisRateLimiter>();

        // L2: Redis IDistributedCache, sharing the multiplexer above (also backs ASP.NET Core Session).
        builder.Services.AddStackExchangeRedisCache(o => o.InstanceName = $"{options.ServiceKeyPrefix}l2:");
        builder.Services.AddOptions<RedisCacheOptions>()
            .Configure<IServiceProvider>((o, sp) =>
                o.ConnectionMultiplexerFactory = () => Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>()));

        // L1 + L2 orchestration with stampede protection and tag invalidation.
        builder.Services.AddHybridCache(o =>
        {
            o.MaximumPayloadBytes = options.MaximumPayloadBytes;
            o.MaximumKeyLength = 512;
            o.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = options.DefaultExpiration,
                LocalCacheExpiration = options.DefaultLocalExpiration,
            };
        });
        builder.Services.TryAddSingleton<IAppCache, HybridAppCache>();

        // Invalidation fan-out.
        builder.Services.AddSingleton<ICacheInvalidationHandler, HybridCacheInvalidationHandler>();
        builder.Services.TryAddSingleton<ICacheInvalidator, RedisCacheInvalidator>();
        builder.Services.AddHostedService<CacheInvalidationSubscriber>();

        // CDN purge.
        if (string.Equals(options.Cdn.Provider, "Cloudflare", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddHttpClient<ICdnPurger, CloudflareCdnPurger>(c =>
            {
                c.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");
                c.Timeout = TimeSpan.FromSeconds(10);
            });
        }
        else
        {
            builder.Services.TryAddSingleton<ICdnPurger, NoOpCdnPurger>();
        }

        builder.Services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
        return builder;
    }
}
