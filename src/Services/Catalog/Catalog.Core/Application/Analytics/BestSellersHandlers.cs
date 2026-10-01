using Catalog.Core.Application.Abstractions;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Application.Products;
using EShop.SharedKernel.Integration;

namespace Catalog.Core.Application.Analytics;

/// <summary>Reads the leaderboard (Sorted Set) and hydrates names from the product cache (L1 hits).</summary>
public sealed class GetBestSellersHandler(IBestSellerRanking ranking, GetProductByIdHandler products, TimeProvider clock)
{
    public async Task<IReadOnlyList<BestSellerDto>> HandleAsync(int days, int top, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, 1, 30);
        top = Math.Clamp(top, 1, 50);

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var ranked = await ranking.TopAsync(today, days, top).ConfigureAwait(false);

        var result = new List<BestSellerDto>(ranked.Count);
        foreach (var (productId, units) in ranked)
        {
            var product = await products.HandleAsync(productId, cancellationToken).ConfigureAwait(false);
            if (product.IsSuccess)
            {
                result.Add(new BestSellerDto(result.Count + 1, productId, product.Value.Name, product.Value.Price, units));
            }
        }

        return result;
    }
}

/// <summary>Projects a checkout event (from the Basket Redis Stream) into today's leaderboard.</summary>
public sealed class ProjectCheckoutToBestSellersHandler(IBestSellerRanking ranking)
{
    public Task HandleAsync(IReadOnlyList<CheckoutLineContract> lines, DateTimeOffset occurredAt) =>
        ranking.IncrementAsync(
            DateOnly.FromDateTime(occurredAt.UtcDateTime),
            lines.Select(l => (l.ProductId, l.Quantity)));
}
