namespace EShop.Caching.Invalidation;

/// <summary>Payload broadcast over Redis Pub/Sub. <see cref="Origin"/> lets the sender ignore its own echo.</summary>
public sealed record CacheInvalidationMessage(string Origin, string[] Tags, string[] Keys);

/// <summary>Identifies this process among all nodes subscribed to the invalidation channel.</summary>
public sealed class CacheNodeIdentity
{
    public string Id { get; } = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
}

/// <summary>A cache layer that can drop entries by tag or key on this node (L1, output cache, ...).</summary>
public interface ICacheInvalidationHandler
{
    ValueTask HandleAsync(CacheInvalidation invalidation, CancellationToken cancellationToken);
}
