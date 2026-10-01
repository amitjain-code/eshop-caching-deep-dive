using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

internal sealed class CatalogReadRepository(CatalogDbContext db) : ICatalogReadRepository
{
    public Task<ProductDto?> GetProductAsync(int id, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description, p.Price, p.Sku,
                p.BrandId, p.Brand!.Name, p.CategoryId, p.Category!.Name,
                p.AvailableStock, p.PictureUrl, p.UpdatedAt, p.Version))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ProductListDto> GetProductsAsync(int page, int pageSize, int? categoryId, int? brandId, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking();
        if (categoryId is { } c)
        {
            query = query.Where(p => p.CategoryId == c);
        }

        if (brandId is { } b)
        {
            query = query.Where(p => p.BrandId == b);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Price, p.Brand!.Name, p.PictureUrl))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new ProductListDto(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<CategoryRow>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking()
            .Select(c => new CategoryRow(c.Id, c.Name, c.Slug, c.DisplayOrder, c.ParentId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken cancellationToken) =>
        await db.Brands.AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<StoreDto>> GetStoresAsync(CancellationToken cancellationToken) =>
        await db.Stores.AsNoTracking()
            .Select(s => new StoreDto(s.Id, s.Name, s.City, s.Latitude, s.Longitude))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}
