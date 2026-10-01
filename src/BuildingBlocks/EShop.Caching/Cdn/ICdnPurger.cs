namespace EShop.Caching.Cdn;

/// <summary>
/// Purges content from the CDN edge. Tag-based purge (Cloudflare Cache-Tag, Fastly Surrogate-Key,
/// Akamai Edge-Cache-Tag) lets one write invalidate every URL that rendered the changed entity.
/// </summary>
public interface ICdnPurger
{
    Task PurgeTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default);
}
