using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Caching;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Domain;
using EShop.Caching;
using EShop.SharedKernel;

namespace Catalog.Core.Application.Products;

/// <summary>
/// Cache-aside on a hot entity: L1 (1 min) -> L2 Redis (10 min) -> PostgreSQL.
/// Misses for unknown ids are cached too (negative caching) so random ids cannot hammer the database
/// (cache penetration).
/// </summary>
public sealed class GetProductByIdHandler(ICatalogReadRepository repository, IAppCache cache)
{
    public async Task<Result<ProductDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return CatalogErrors.InvalidProductId;
        }

        var product = await cache.GetOrCreateAsync(
            CatalogCache.Keys.Product(id),
            async ct => await repository.GetProductAsync(id, ct).ConfigureAwait(false),
            CachePolicy.HotEntity,
            [CatalogCache.Tags.Product(id), CatalogCache.Tags.Products],
            cancellationToken).ConfigureAwait(false);

        if (product is null)
        {
            return CatalogErrors.ProductNotFound(id);
        }

        return product;
    }
}
