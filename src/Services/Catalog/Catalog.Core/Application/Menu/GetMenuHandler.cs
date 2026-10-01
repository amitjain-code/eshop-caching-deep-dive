using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Caching;
using Catalog.Core.Application.Dtos;
using EShop.Caching;

namespace Catalog.Core.Application.Menu;

/// <summary>
/// The navigation menu is rendered on every page: perfect for a long-lived L1 copy on each node
/// (reference data). Building the category tree is done once per miss, not per request.
/// </summary>
public sealed class GetMenuHandler(ICatalogReadRepository repository, IAppCache cache, TimeProvider clock)
{
    public async Task<MenuDto> HandleAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            CatalogCache.Keys.Menu,
            async ct =>
            {
                var categories = await repository.GetCategoriesAsync(ct).ConfigureAwait(false);
                var brands = await repository.GetBrandsAsync(ct).ConfigureAwait(false);
                return new MenuDto(BuildTree(categories, parentId: null), brands, clock.GetUtcNow());
            },
            CachePolicy.ReferenceData,
            [CatalogCache.Tags.Menu],
            cancellationToken).ConfigureAwait(false);

    internal static IReadOnlyList<MenuNodeDto> BuildTree(IReadOnlyList<CategoryRow> rows, int? parentId) =>
        [.. rows
            .Where(r => r.ParentId == parentId)
            .OrderBy(r => r.DisplayOrder)
            .Select(r => new MenuNodeDto(r.Id, r.Name, r.Slug, BuildTree(rows, r.Id)))];
}
