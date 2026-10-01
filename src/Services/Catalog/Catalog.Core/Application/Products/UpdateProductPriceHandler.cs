using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Caching;
using Catalog.Core.Domain;
using EShop.Caching;
using EShop.SharedKernel;

namespace Catalog.Core.Application.Products;

/// <summary>
/// Write path: update the source of truth first, then invalidate every layer
/// (L1 on all nodes via Pub/Sub, L2 Redis, gateway output cache, CDN by tag).
/// Browser caches cannot be purged - that is why public max-age stays short (60 s).
/// </summary>
public sealed class UpdateProductPriceHandler(IProductRepository products, ICacheInvalidator invalidator, TimeProvider clock)
{
    public async Task<Result> HandleAsync(int id, decimal newPrice, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return CatalogErrors.ProductNotFound(id);
        }

        var change = product.ChangePrice(newPrice, clock.GetUtcNow());
        if (change.IsFailure)
        {
            return change;
        }

        var saved = await products.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return saved;
        }

        await invalidator.InvalidateAsync(
            new CacheInvalidation(
                Tags: [CatalogCache.Tags.Product(id), CatalogCache.Tags.Products],
                Keys: [CatalogCache.Keys.Product(id)]),
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
