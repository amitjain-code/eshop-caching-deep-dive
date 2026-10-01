namespace EShop.Caching;

/// <summary>
/// Application-facing cache-aside port. The default implementation is a two-level cache
/// (in-process L1 + Redis L2) built on <c>HybridCache</c>, with stampede protection.
/// </summary>
public interface IAppCache
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/> or runs <paramref name="factory"/> exactly once
    /// per key per process (concurrent callers wait for the same result) and caches the outcome.
    /// </summary>
    ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CachePolicy policy,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default);

    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
