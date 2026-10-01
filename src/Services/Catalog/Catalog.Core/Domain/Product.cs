using EShop.SharedKernel;

namespace Catalog.Core.Domain;

public sealed class Product : Entity<int>
{
    private Product()
    {
    }

    public Product(string name, string description, decimal price, string sku, int brandId, int categoryId, int availableStock, string? pictureUrl, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        Name = name;
        Description = description;
        Price = price;
        Sku = sku;
        BrandId = brandId;
        CategoryId = categoryId;
        AvailableStock = availableStock;
        PictureUrl = pictureUrl;
        UpdatedAt = now;
        Version = 1;
    }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public int BrandId { get; private set; }

    public Brand? Brand { get; private set; }

    public int CategoryId { get; private set; }

    public Category? Category { get; private set; }

    public int AvailableStock { get; private set; }

    public string? PictureUrl { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token and the source of the product's ETag.</summary>
    public int Version { get; private set; }

    public Result ChangePrice(decimal newPrice, DateTimeOffset now)
    {
        if (newPrice <= 0 || newPrice > 1_000_000)
        {
            return CatalogErrors.InvalidPrice;
        }

        if (newPrice == Price)
        {
            return Result.Success();
        }

        Price = newPrice;
        UpdatedAt = now;
        Version++;
        return Result.Success();
    }
}
