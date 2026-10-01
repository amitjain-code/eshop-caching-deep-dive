using Catalog.Core.Application.Dtos;
using Catalog.Core.Domain;
using EShop.SharedKernel;

namespace Catalog.Core.Application.Abstractions;

/// <summary>Read side: projections straight to DTOs (no tracking, no aggregates).</summary>
public interface ICatalogReadRepository
{
    Task<ProductDto?> GetProductAsync(int id, CancellationToken cancellationToken);

    Task<ProductListDto> GetProductsAsync(int page, int pageSize, int? categoryId, int? brandId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryRow>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StoreDto>> GetStoresAsync(CancellationToken cancellationToken);
}

/// <summary>Write side: loads aggregates for modification.</summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Redis Sorted Set: product -> units sold, one set per day; ranges are unions of days.</summary>
public interface IBestSellerRanking
{
    Task IncrementAsync(DateOnly day, IEnumerable<(int ProductId, int Quantity)> lines);

    Task<IReadOnlyList<(int ProductId, long UnitsSold)>> TopAsync(DateOnly today, int days, int count);
}

/// <summary>Redis HyperLogLog: approximate unique visitors per product per day in 12 KB per key.</summary>
public interface IProductViewCounter
{
    Task RecordAsync(int productId, string visitorId, DateOnly day);

    Task<long> CountUniqueAsync(int productId, DateOnly today, int days);
}

/// <summary>Redis GEO set of store coordinates.</summary>
public interface IStoreLocator
{
    Task IndexAsync(IEnumerable<StoreDto> stores);

    Task<IReadOnlyList<(int StoreId, double DistanceKm)>> FindNearbyAsync(double latitude, double longitude, double radiusKm, int take);
}

/// <summary>Redis Sorted Set with equal scores queried lexicographically (ZRANGEBYLEX) for type-ahead.</summary>
public interface IProductSuggestionIndex
{
    Task IndexAsync(IEnumerable<(int ProductId, string Name)> products);

    Task<IReadOnlyList<SuggestionDto>> SuggestAsync(string prefix, int take);
}
