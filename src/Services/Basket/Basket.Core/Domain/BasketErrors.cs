using EShop.SharedKernel;

namespace Basket.Core.Domain;

public static class BasketErrors
{
    public static readonly Error InvalidProduct = Error.Validation("basket.invalid_product", "Product id must be positive.");
    public static readonly Error InvalidProductName = Error.Validation("basket.invalid_product_name", "Product name is required (max 200 chars).");
    public static readonly Error InvalidPrice = Error.Validation("basket.invalid_price", "Unit price must be positive.");
    public static readonly Error InvalidQuantity = Error.Validation("basket.invalid_quantity", $"Quantity must be between 1 and {CartItem.MaxQuantity}.");
    public static readonly Error ItemNotFound = Error.NotFound("basket.item_not_found", "The product is not in the basket.");
    public static readonly Error EmptyCart = Error.Validation("basket.empty", "The basket is empty.");
    public static readonly Error CheckoutInProgress = Error.Conflict("basket.checkout_in_progress", "A checkout for this basket is already in progress.");
    public static readonly Error SessionNotFound = Error.NotFound("basket.session_not_found", "Session not found or expired.");
}
