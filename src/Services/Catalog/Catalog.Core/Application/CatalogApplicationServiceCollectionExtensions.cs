using Catalog.Core.Application.Analytics;
using Catalog.Core.Application.Menu;
using Catalog.Core.Application.Products;
using Catalog.Core.Application.Search;
using Catalog.Core.Application.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Catalog.Core.Application;

public static class CatalogApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<GetProductByIdHandler>();
        services.AddScoped<GetProductsHandler>();
        services.AddScoped<UpdateProductPriceHandler>();
        services.AddScoped<GetMenuHandler>();
        services.AddScoped<GetBestSellersHandler>();
        services.AddScoped<ProjectCheckoutToBestSellersHandler>();
        services.AddScoped<ProductViewHandlers>();
        services.AddScoped<FindNearbyStoresHandler>();
        services.AddScoped<SuggestProductsHandler>();
        return services;
    }
}
