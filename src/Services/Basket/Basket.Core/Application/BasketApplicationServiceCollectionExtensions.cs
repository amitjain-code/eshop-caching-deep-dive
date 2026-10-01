using Basket.Core.Application.Analytics;
using Basket.Core.Application.Cart;
using Basket.Core.Application.Checkout;
using Basket.Core.Application.Engagement;
using Basket.Core.Application.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Basket.Core.Application;

public static class BasketApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBasketApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<CartHandlers>();
        services.AddScoped<CheckoutHandler>();
        services.AddScoped<RecentlyViewedHandlers>();
        services.AddScoped<WishlistHandlers>();
        services.AddScoped<SessionHandlers>();
        services.AddScoped<ActiveUsersHandler>();
        return services;
    }
}
