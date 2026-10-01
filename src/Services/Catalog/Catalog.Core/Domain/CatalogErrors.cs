using EShop.SharedKernel;

namespace Catalog.Core.Domain;

public static class CatalogErrors
{
    public static readonly Error InvalidPrice = Error.Validation("catalog.invalid_price", "Price must be between 0 and 1,000,000.");

    public static readonly Error InvalidProductId = Error.Validation("catalog.invalid_product_id", "Product id must be positive.");

    public static readonly Error InvalidPaging = Error.Validation("catalog.invalid_paging", "Page must be >= 1 and pageSize between 1 and 50.");

    public static readonly Error InvalidCoordinates = Error.Validation("catalog.invalid_coordinates", "Latitude/longitude out of range or radius not in 1..500 km.");

    public static readonly Error InvalidQuery = Error.Validation("catalog.invalid_query", "Search prefix must be 2 to 50 characters.");

    public static readonly Error ConcurrencyConflict = Error.Conflict("catalog.concurrency", "The product was modified by someone else. Reload and retry.");

    public static Error ProductNotFound(int id) => Error.NotFound("catalog.product_not_found", $"Product {id} was not found.");
}
