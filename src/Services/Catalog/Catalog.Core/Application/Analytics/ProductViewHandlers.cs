using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Domain;
using EShop.SharedKernel;

namespace Catalog.Core.Application.Analytics;

/// <summary>
/// Unique visitors per product. A Set of visitor ids would grow without bound (millions of members);
/// HyperLogLog answers "how many distinct" in a fixed 12 KB with ~0.81% standard error.
/// </summary>
public sealed class ProductViewHandlers(IProductViewCounter counter, TimeProvider clock)
{
    public async Task<Result> RecordAsync(int productId, string visitorId)
    {
        if (productId <= 0)
        {
            return CatalogErrors.InvalidProductId;
        }

        await counter.RecordAsync(productId, visitorId, Today()).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<UniqueViewsDto>> CountAsync(int productId, int days)
    {
        if (productId <= 0)
        {
            return CatalogErrors.InvalidProductId;
        }

        days = Math.Clamp(days, 1, 90);
        var unique = await counter.CountUniqueAsync(productId, Today(), days).ConfigureAwait(false);
        return new UniqueViewsDto(productId, days, unique);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
