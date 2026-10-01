namespace EShop.Caching;

/// <summary>
/// Expiration policy for an <see cref="IAppCache"/> entry.
/// </summary>
/// <param name="Expiration">Lifetime in the distributed (Redis) L2 cache.</param>
/// <param name="LocalExpiration">Lifetime in the in-process L1 cache. Keep it short: L1 is per node and
/// is the part most likely to serve stale data.</param>
/// <param name="JitterFactor">Random extra lifetime (0.1 = up to +10%) so that keys written together do not
/// all expire together and stampede the database.</param>
public sealed record CachePolicy(TimeSpan Expiration, TimeSpan LocalExpiration, double JitterFactor = 0.1)
{
    /// <summary>Menus, categories, brands: change rarely, read on every page.</summary>
    public static CachePolicy ReferenceData { get; } = new(TimeSpan.FromHours(6), TimeSpan.FromMinutes(10));

    /// <summary>Single hot entity such as a product detail page.</summary>
    public static CachePolicy HotEntity { get; } = new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1));

    /// <summary>Paged listings and search results: many key permutations, short life.</summary>
    public static CachePolicy Listing { get; } = new(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30));

    /// <summary>Returns a copy of the expirations with random jitter applied.</summary>
    public (TimeSpan Expiration, TimeSpan LocalExpiration) WithJitter(Random? random = null)
    {
        if (JitterFactor <= 0)
        {
            return (Expiration, LocalExpiration);
        }

        var factor = 1 + ((random ?? Random.Shared).NextDouble() * JitterFactor);
        var local = LocalExpiration * factor;
        var distributed = Expiration * factor;
        return (distributed, local <= distributed ? local : distributed);
    }
}
