using Catalog.Core.Application.Abstractions;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Catalog.Infrastructure.Hosting;

/// <summary>
/// Creates/seeds the database (demo only - use migrations in production) and warms the Redis
/// indexes (GEO + suggestions). Warming is idempotent: GEOADD/ZADD overwrite existing members.
/// </summary>
internal sealed partial class CatalogStartupInitializer(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<CatalogStartupInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 10 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await InitializeAsync(stoppingToken).ConfigureAwait(false);
                LogWarmed(logger);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRetry(logger, ex, attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempt * 3)), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await CatalogSeeder.SeedAsync(db, clock, cancellationToken).ConfigureAwait(false);

        var reader = scope.ServiceProvider.GetRequiredService<ICatalogReadRepository>();
        var stores = await reader.GetStoresAsync(cancellationToken).ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<IStoreLocator>().IndexAsync(stores).ConfigureAwait(false);

        var products = await db.Products.AsNoTracking()
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<IProductSuggestionIndex>()
            .IndexAsync(products.Select(p => (p.Id, p.Name))).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog database seeded and Redis indexes warmed")]
    private static partial void LogWarmed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog initialization attempt {Attempt} failed; retrying")]
    private static partial void LogRetry(ILogger logger, Exception exception, int attempt);
}
