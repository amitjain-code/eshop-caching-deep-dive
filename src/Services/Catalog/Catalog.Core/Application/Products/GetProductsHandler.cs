using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Caching;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Domain;
using EShop.Caching;
using EShop.SharedKernel;

namespace Catalog.Core.Application.Products;

/// <summary>
/// Listings have many key permutations (page x size x filters) and change whenever any product changes,
/// so they get a short lifetime and the broad "products" tag for bulk invalidation.
/// </summary>
public sealed class GetProductsHandler(ICatalogReadRepository repository, IAppCache cache)
{
    public const int MaxPageSize = 50;

    public async Task<Result<ProductListDto>> HandleAsync(int page, int pageSize, int? categoryId, int? brandId, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return CatalogErrors.InvalidPaging;
        }

        return await cache.GetOrCreateAsync(
            CatalogCache.Keys.ProductPage(page, pageSize, categoryId, brandId),
            async ct => await repository.GetProductsAsync(page, pageSize, categoryId, brandId, ct).ConfigureAwait(false),
            CachePolicy.Listing,
            [CatalogCache.Tags.Products],
            cancellationToken).ConfigureAwait(false);
    }
}
