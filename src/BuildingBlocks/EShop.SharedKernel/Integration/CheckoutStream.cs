namespace EShop.SharedKernel.Integration;

/// <summary>
/// Contract for the Redis Stream that carries checkout events from Basket to downstream consumers
/// (Catalog best-seller projection, Ordering, Analytics). Streams are an append-only, replayable log:
/// unlike Pub/Sub, a consumer that is offline does not lose messages.
/// </summary>
public static class CheckoutStream
{
    /// <summary>Un-prefixed key shared across services.</summary>
    public const string Key = "eshop:streams:checkout";

    /// <summary>Upper bound kept with approximate trimming (XADD MAXLEN ~).</summary>
    public const int MaxLength = 100_000;

    public static class Fields
    {
        public const string CheckoutId = "checkoutId";
        public const string BuyerId = "buyerId";
        public const string Items = "items";
        public const string Total = "total";
        public const string OccurredAt = "occurredAt";
    }
}

public sealed record CheckoutLineContract(int ProductId, int Quantity, decimal UnitPrice);
