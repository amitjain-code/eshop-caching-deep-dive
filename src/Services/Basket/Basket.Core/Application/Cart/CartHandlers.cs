using Basket.Core.Application.Abstractions;
using Basket.Core.Domain;
using EShop.SharedKernel;

namespace Basket.Core.Application.Cart;

/// <summary>
/// The cart is <b>primary data that lives in Redis</b> (not a cache of a database row): it is hot,
/// per-user, short-lived and can be rebuilt by the shopper. Persistence (AOF everysec) and a TTL
/// keep it safe and bounded.
/// </summary>
public sealed class CartHandlers(ICartRepository carts)
{
    public async Task<CartDto> GetAsync(string buyerId) =>
        CartDto.From(await carts.GetAsync(buyerId).ConfigureAwait(false));

    public async Task<Result<CartDto>> AddAsync(string buyerId, AddCartItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = CartItem.Create(request.ProductId, request.ProductName, request.UnitPrice, request.Quantity, request.PictureUrl);
        if (item.IsFailure)
        {
            return item.Error!;
        }

        var newQuantity = await carts.AddOrIncrementAsync(buyerId, item.Value).ConfigureAwait(false);
        if (newQuantity > CartItem.MaxQuantity)
        {
            await carts.SetQuantityAsync(buyerId, request.ProductId, CartItem.MaxQuantity).ConfigureAwait(false);
        }

        return await GetAsync(buyerId).ConfigureAwait(false);
    }

    public async Task<Result<CartDto>> SetQuantityAsync(string buyerId, int productId, int quantity)
    {
        if (quantity is < 0 or > CartItem.MaxQuantity)
        {
            return BasketErrors.InvalidQuantity;
        }

        if (!await carts.SetQuantityAsync(buyerId, productId, quantity).ConfigureAwait(false))
        {
            return BasketErrors.ItemNotFound;
        }

        return await GetAsync(buyerId).ConfigureAwait(false);
    }

    public async Task<Result<CartDto>> RemoveAsync(string buyerId, int productId)
    {
        if (!await carts.RemoveAsync(buyerId, productId).ConfigureAwait(false))
        {
            return BasketErrors.ItemNotFound;
        }

        return await GetAsync(buyerId).ConfigureAwait(false);
    }

    public Task ClearAsync(string buyerId) => carts.DeleteAsync(buyerId);
}
