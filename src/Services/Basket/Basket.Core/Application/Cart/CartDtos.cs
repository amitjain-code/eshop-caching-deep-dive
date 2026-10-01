using Basket.Core.Domain;

namespace Basket.Core.Application.Cart;

public sealed record CartItemDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal, string? PictureUrl);

public sealed record CartDto(string BuyerId, IReadOnlyList<CartItemDto> Items, int TotalQuantity, decimal Total)
{
    public static CartDto From(ShoppingCart cart)
    {
        ArgumentNullException.ThrowIfNull(cart);
        return new CartDto(
            cart.BuyerId,
            [.. cart.Items.Select(i => new CartItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal, i.PictureUrl))],
            cart.TotalQuantity,
            cart.Total);
    }
}

public sealed record AddCartItemRequest(int ProductId, string ProductName, decimal UnitPrice, int Quantity, string? PictureUrl);
