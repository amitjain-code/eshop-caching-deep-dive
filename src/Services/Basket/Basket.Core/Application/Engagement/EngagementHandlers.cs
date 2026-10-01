using Basket.Core.Application.Abstractions;
using Basket.Core.Domain;
using EShop.SharedKernel;

namespace Basket.Core.Application.Engagement;

public sealed record ProductIdsDto(IReadOnlyList<int> ProductIds);

public sealed class RecentlyViewedHandlers(IRecentlyViewedStore store)
{
    public const int Capacity = 20;

    public async Task<Result> AddAsync(string userId, int productId)
    {
        if (productId <= 0)
        {
            return BasketErrors.InvalidProduct;
        }

        await store.AddAsync(userId, productId).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<ProductIdsDto> GetAsync(string userId, int take) =>
        new(await store.GetAsync(userId, Math.Clamp(take, 1, Capacity)).ConfigureAwait(false));
}

public sealed class WishlistHandlers(IWishlistStore store)
{
    public async Task<Result<bool>> AddAsync(string userId, int productId) =>
        productId <= 0 ? BasketErrors.InvalidProduct : await store.AddAsync(userId, productId).ConfigureAwait(false);

    public Task<bool> RemoveAsync(string userId, int productId) => store.RemoveAsync(userId, productId);

    public Task<bool> ContainsAsync(string userId, int productId) => store.ContainsAsync(userId, productId);

    public async Task<ProductIdsDto> GetAsync(string userId) => new(await store.GetAsync(userId).ConfigureAwait(false));

    public async Task<ProductIdsDto> CommonAsync(string userId, string otherUserId) =>
        new(await store.CommonAsync(userId, otherUserId).ConfigureAwait(false));
}
