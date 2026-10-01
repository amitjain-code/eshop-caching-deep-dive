using Catalog.Core.Application.Abstractions;
using Catalog.Infrastructure.Hosting;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Catalog.Infrastructure;

public static class CatalogInfrastructureExtensions
{
    public static IHostApplicationBuilder AddCatalogInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration.GetConnectionString("catalogdb")
            ?? throw new InvalidOperationException("ConnectionStrings:catalogdb is required.");

        builder.Services.AddDbContextPool<CatalogDbContext>(o =>
            o.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

        builder.Services.AddScoped<ICatalogReadRepository, CatalogReadRepository>();
        builder.Services.AddScoped<IProductRepository, ProductRepository>();

        builder.Services.AddSingleton<IBestSellerRanking, RedisBestSellerRanking>();
        builder.Services.AddSingleton<IProductViewCounter, RedisProductViewCounter>();
        builder.Services.AddSingleton<IStoreLocator, RedisStoreLocator>();
        builder.Services.AddSingleton<IProductSuggestionIndex, RedisProductSuggestionIndex>();

        builder.Services.AddHostedService<CatalogStartupInitializer>();
        builder.Services.AddHostedService<BestSellerProjector>();

        builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("catalogdb", tags: ["ready"]);
        return builder;
    }
}
