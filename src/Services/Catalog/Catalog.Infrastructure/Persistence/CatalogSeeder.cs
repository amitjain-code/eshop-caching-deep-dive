using Catalog.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

internal static class CatalogSeeder
{
    public static async Task SeedAsync(CatalogDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var now = clock.GetUtcNow();

        var brands = new[] { "Contoso", "Fabrikam", "Northwind", "AdventureWorks", "Tailspin" }
            .Select(n => new Brand(n)).ToArray();
        db.Brands.AddRange(brands);

        var clothing = new Category("Clothing", "clothing", 1);
        var gear = new Category("Outdoor Gear", "outdoor-gear", 2);
        var electronics = new Category("Electronics", "electronics", 3);
        db.Categories.AddRange(clothing, gear, electronics);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var jackets = new Category("Jackets", "jackets", 1, clothing.Id);
        var shoes = new Category("Shoes", "shoes", 2, clothing.Id);
        var tents = new Category("Tents", "tents", 1, gear.Id);
        var backpacks = new Category("Backpacks", "backpacks", 2, gear.Id);
        var headphones = new Category("Headphones", "headphones", 1, electronics.Id);
        var leaves = new[] { jackets, shoes, tents, backpacks, headphones };
        db.Categories.AddRange(leaves);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        string[] adjectives = ["Alpine", "Trail", "Summit", "Urban", "Storm", "Glacier", "Canyon", "Coastal"];
        var random = new Random(42); // deterministic demo data
        var products = new List<Product>();
        var sku = 1000;
        foreach (var category in leaves)
        {
            foreach (var adjective in adjectives)
            {
                var brand = brands[random.Next(brands.Length)];
                var name = $"{adjective} {category.Name.TrimEnd('s')} {brand.Name}";
                products.Add(new Product(
                    name,
                    $"{name} - demo product used to explore multi-level caching.",
                    Math.Round((decimal)(random.NextDouble() * 280) + 19.99m, 2),
                    $"SKU-{sku++}",
                    brand.Id,
                    category.Id,
                    random.Next(0, 250),
                    $"https://cdn.example.com/img/{sku}.webp",
                    now));
            }
        }

        db.Products.AddRange(products);

        db.Stores.AddRange(
            new Store("eShop Connaught Place", "New Delhi", 28.6315, 77.2167),
            new Store("eShop Cyber Hub", "Gurugram", 28.4950, 77.0890),
            new Store("eShop Noida Sector 18", "Noida", 28.5708, 77.3261),
            new Store("eShop Bandra", "Mumbai", 19.0596, 72.8295),
            new Store("eShop Koramangala", "Bengaluru", 12.9352, 77.6245),
            new Store("eShop Hitech City", "Hyderabad", 17.4435, 78.3772),
            new Store("eShop Park Street", "Kolkata", 22.5535, 88.3520),
            new Store("eShop T Nagar", "Chennai", 13.0418, 80.2341));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
