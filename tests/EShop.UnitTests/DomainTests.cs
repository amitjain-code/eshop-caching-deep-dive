using Basket.Core.Domain;
using Catalog.Core.Application.Dtos;
using Catalog.Core.Application.Menu;
using Catalog.Core.Domain;

namespace EShop.UnitTests;

public sealed class DomainTests
{
    [Fact]
    public void Product_price_change_bumps_version_used_for_etag()
    {
        var product = new Product("Alpine Jacket", "", 100m, "SKU-1", 1, 1, 5, null, DateTimeOffset.UnixEpoch);

        var result = product.ChangePrice(120m, DateTimeOffset.UnixEpoch.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, product.Version);
        Assert.Equal(120m, product.Price);
    }

    [Fact]
    public void Same_price_does_not_bump_version()
    {
        var product = new Product("Alpine Jacket", "", 100m, "SKU-1", 1, 1, 5, null, DateTimeOffset.UnixEpoch);

        product.ChangePrice(100m, DateTimeOffset.UnixEpoch);

        Assert.Equal(1, product.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Invalid_price_is_rejected(int price)
    {
        var product = new Product("Alpine Jacket", "", 100m, "SKU-1", 1, 1, 5, null, DateTimeOffset.UnixEpoch);

        Assert.Equal(CatalogErrors.InvalidPrice, product.ChangePrice(price, DateTimeOffset.UnixEpoch).Error);
    }

    [Fact]
    public void Menu_tree_is_built_from_flat_rows_in_display_order()
    {
        CategoryRow[] rows =
        [
            new(1, "Clothing", "clothing", 2, null),
            new(2, "Gear", "gear", 1, null),
            new(3, "Shoes", "shoes", 2, 1),
            new(4, "Jackets", "jackets", 1, 1),
        ];

        var tree = GetMenuHandler.BuildTree(rows, parentId: null);

        Assert.Equal(new[] { "Gear", "Clothing" }, tree.Select(n => n.Name));
        Assert.Equal(new[] { "Jackets", "Shoes" }, tree[1].Children.Select(n => n.Name));
    }

    [Fact]
    public void Cart_totals_and_validation()
    {
        var cart = new ShoppingCart("alice",
        [
            new CartItem(1, "Tent", 199.99m, 2, null),
            new CartItem(2, "Boots", 89.50m, 1, null),
        ]);

        Assert.Equal(489.48m, cart.Total);
        Assert.Equal(3, cart.TotalQuantity);
        Assert.Equal(BasketErrors.InvalidQuantity, CartItem.Create(1, "Tent", 10m, 100, null).Error);
    }
}
