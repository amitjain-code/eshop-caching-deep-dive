using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Caching;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Domain;
using EShop.Caching;
using EShop.SharedKernel;

namespace Catalog.Core.Application.Stores;

/// <summary>GEOSEARCH for the ids + distances, store details from the reference-data cache.</summary>
public sealed class FindNearbyStoresHandler(IStoreLocator locator, ICatalogReadRepository repository, IAppCache cache)
{
    public async Task<Result<IReadOnlyList<NearbyStoreDto>>> HandleAsync(double latitude, double longitude, double radiusKm, CancellationToken cancellationToken)
    {
        if (Math.Abs(latitude) > 85.05 || Math.Abs(longitude) > 180 || radiusKm is < 1 or > 500)
        {
            return CatalogErrors.InvalidCoordinates;
        }

        var nearby = await locator.FindNearbyAsync(latitude, longitude, radiusKm, take: 10).ConfigureAwait(false);
        var stores = await cache.GetOrCreateAsync(
            CatalogCache.Keys.Stores,
            async ct => await repository.GetStoresAsync(ct).ConfigureAwait(false),
            CachePolicy.ReferenceData,
            [CatalogCache.Tags.Stores],
            cancellationToken).ConfigureAwait(false);

        var byId = stores.ToDictionary(s => s.Id);
        IReadOnlyList<NearbyStoreDto> result =
        [
            .. nearby
                .Where(n => byId.ContainsKey(n.StoreId))
                .Select(n => new NearbyStoreDto(n.StoreId, byId[n.StoreId].Name, byId[n.StoreId].City, Math.Round(n.DistanceKm, 2))),
        ];
        return Result<IReadOnlyList<NearbyStoreDto>>.Success(result);
    }
}
