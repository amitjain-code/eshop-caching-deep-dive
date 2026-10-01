namespace EShop.Caching;

/// <summary>Short-lived mutual exclusion across service instances (Redis SET NX PX).</summary>
public interface IDistributedLock
{
    /// <summary>Returns a handle that releases the lock when disposed, or <c>null</c> when the lock is held elsewhere.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string resource, TimeSpan lease);
}
