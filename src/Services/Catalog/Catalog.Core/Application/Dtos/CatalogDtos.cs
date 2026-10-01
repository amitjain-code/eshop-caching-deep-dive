using System.Globalization;
using Catalog.Core.Application.Caching;
using EShop.Caching;

namespace Catalog.Core.Application.Dtos;

public sealed record ProductDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string Sku,
    int BrandId,
    string Brand,
    int CategoryId,
    string Category,
    int AvailableStock,
    string? PictureUrl,
    DateTimeOffset UpdatedAt,
    int Version) : IVersionedResource, IHasCacheTags, IHasLastModified
{
    string IVersionedResource.ResourceVersion => string.Create(CultureInfo.InvariantCulture, $"{Id}-v{Version}");

    IEnumerable<string> IHasCacheTags.CacheTags => [CatalogCache.Tags.Product(Id)];

    DateTimeOffset IHasLastModified.LastModified => UpdatedAt;
}

public sealed record ProductSummaryDto(int Id, string Name, decimal Price, string Brand, string? PictureUrl);

public sealed record ProductListDto(IReadOnlyList<ProductSummaryDto> Items, int Page, int PageSize, long TotalCount) : IHasCacheTags
{
    IEnumerable<string> IHasCacheTags.CacheTags =>
        Items.Select(i => CatalogCache.Tags.Product(i.Id)).Prepend(CatalogCache.Tags.Products);
}

public sealed record MenuNodeDto(int Id, string Name, string Slug, IReadOnlyList<MenuNodeDto> Children);

public sealed record BrandDto(int Id, string Name);

public sealed record MenuDto(IReadOnlyList<MenuNodeDto> Categories, IReadOnlyList<BrandDto> Brands, DateTimeOffset GeneratedAt) : IHasCacheTags
{
    IEnumerable<string> IHasCacheTags.CacheTags => [CatalogCache.Tags.Menu];
}

public sealed record CategoryRow(int Id, string Name, string Slug, int DisplayOrder, int? ParentId);

public sealed record StoreDto(int Id, string Name, string City, double Latitude, double Longitude);

public sealed record NearbyStoreDto(int StoreId, string Name, string City, double DistanceKm);

public sealed record BestSellerDto(int Rank, int ProductId, string Name, decimal Price, long UnitsSold);

public sealed record SuggestionDto(int ProductId, string Name);

public sealed record UniqueViewsDto(int ProductId, int Days, long UniqueVisitors, string Accuracy = "approx ±0.81% (HyperLogLog)");
