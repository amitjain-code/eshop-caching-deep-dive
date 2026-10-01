using Basket.Core.Application.Abstractions;
using Basket.Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Basket.Infrastructure;

public static class BasketInfrastructureExtensions
{
    public static IHostApplicationBuilder AddBasketInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ICartRepository, RedisCartRepository>();
        builder.Services.AddSingleton<IRecentlyViewedStore, RedisRecentlyViewedStore>();
        builder.Services.AddSingleton<IWishlistStore, RedisWishlistStore>();
        builder.Services.AddSingleton<IUserSessionStore, RedisUserSessionStore>();
        builder.Services.AddSingleton<IActiveUserTracker, RedisActiveUserTracker>();
        builder.Services.AddSingleton<ICheckoutEventPublisher, RedisCheckoutEventPublisher>();
        return builder;
    }
}
