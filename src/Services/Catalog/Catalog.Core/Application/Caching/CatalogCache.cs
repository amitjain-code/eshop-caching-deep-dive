using System.Globalization;

namespace Catalog.Core.Application.Caching;

/// <summary>
/// Single source of truth for Catalog cache keys and tags.
/// Keys address one entry; tags group entries so one write can invalidate many keys
/// (and the matching gateway / CDN entries, which carry the same tags in <c>Cache-Tag</c>).
/// </summary>
public static class CatalogCache
{
    public static class Keys
    {
        public const string Menu = "catalog:menu";
        public const string Stores = "catalog:stores";

        public static string Product(int id) => string.Create(CultureInfo.InvariantCulture, $"catalog:product:{id}");

        public static string ProductPage(int page, int pageSize, int? categoryId, int? brandId) =>
            string.Create(CultureInfo.InvariantCulture, $"catalog:products:p{page}:s{pageSize}:c{categoryId?.ToString(CultureInfo.InvariantCulture) ?? "*"}:b{brandId?.ToString(CultureInfo.InvariantCulture) ?? "*"}");
    }

    public static class Tags
    {
        public const string Products = "products";
        public const string Menu = "menu";
        public const string Stores = "stores";

        public static string Product(int id) => string.Create(CultureInfo.InvariantCulture, $"product:{id}");
    }
}
