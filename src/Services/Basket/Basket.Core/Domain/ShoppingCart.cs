using EShop.SharedKernel;

namespace Basket.Core.Domain;

/// <summary>A line in the cart. Price and name are a snapshot taken when the item was added.</summary>
public sealed record CartItem(int ProductId, string ProductName, decimal UnitPrice, int Quantity, string? PictureUrl)
{
    public const int MaxQuantity = 99;

    public decimal LineTotal => UnitPrice * Quantity;

    public static Result<CartItem> Create(int productId, string productName, decimal unitPrice, int quantity, string? pictureUrl)
    {
        if (productId <= 0)
        {
            return BasketErrors.InvalidProduct;
        }

        if (string.IsNullOrWhiteSpace(productName) || productName.Length > 200)
        {
            return BasketErrors.InvalidProductName;
        }

        if (unitPrice <= 0)
        {
            return BasketErrors.InvalidPrice;
        }

        if (quantity is < 1 or > MaxQuantity)
        {
            return BasketErrors.InvalidQuantity;
        }

        return new CartItem(productId, productName.Trim(), unitPrice, quantity, pictureUrl);
    }
}

/// <summary>
/// Read model of a buyer's cart. Persisted as a Redis Hash so each line can be changed atomically
/// without rewriting (or racing on) the whole cart document.
/// </summary>
public sealed class ShoppingCart(string buyerId, IEnumerable<CartItem> items)
{
    public string BuyerId { get; } = buyerId;

    public IReadOnlyList<CartItem> Items { get; } = [.. items.Where(i => i.Quantity > 0).OrderBy(i => i.ProductId)];

    public decimal Total => Items.Sum(i => i.LineTotal);

    public int TotalQuantity => Items.Sum(i => i.Quantity);

    public bool IsEmpty => Items.Count == 0;

    public static ShoppingCart Empty(string buyerId) => new(buyerId, []);
}
