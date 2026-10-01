using System.ComponentModel.DataAnnotations;

namespace EShop.Caching.Configuration;

public sealed class CachingOptions
{
    public const string SectionName = "Caching";

    /// <summary>Logical service name; becomes part of every Redis key (eshop:{service}:...).</summary>
    [Required]
    public string ServiceName { get; set; } = "app";

    [Required]
    public string KeyPrefix { get; set; } = "eshop";

    /// <summary>Default lifetime in Redis (L2) when a call does not pass a policy.</summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Default lifetime in process memory (L1).</summary>
    public TimeSpan DefaultLocalExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Entries larger than this are not cached (protects Redis from "big keys").</summary>
    [Range(1024, 64 * 1024 * 1024)]
    public long MaximumPayloadBytes { get; set; } = 1024 * 1024;

    /// <summary>Pub/Sub channel used to broadcast invalidations to every node.</summary>
    [Required]
    public string InvalidationChannel { get; set; } = "eshop:cache-invalidation";

    public CdnOptions Cdn { get; set; } = new();

    /// <summary>Prefix applied to every service-owned Redis key.</summary>
    public string ServiceKeyPrefix => $"{KeyPrefix}:{ServiceName}:";
}

public sealed class CdnOptions
{
    /// <summary>None | Cloudflare. Extend with Fastly, Akamai or Azure Front Door as needed.</summary>
    public string Provider { get; set; } = "None";

    public string? ZoneId { get; set; }

    public string? ApiToken { get; set; }
}
