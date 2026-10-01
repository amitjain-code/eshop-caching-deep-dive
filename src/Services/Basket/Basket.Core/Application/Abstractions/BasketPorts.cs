using Basket.Core.Domain;

namespace Basket.Core.Application.Abstractions;

/// <summary>Redis Hash per buyer: field per line, HINCRBY for atomic quantity changes.</summary>
public interface ICartRepository
{
    Task<ShoppingCart> GetAsync(string buyerId);

    /// <summary>Adds <paramref name="item"/> or increments its quantity; returns the new quantity.</summary>
    Task<long> AddOrIncrementAsync(string buyerId, CartItem item);

    /// <summary>Sets an absolute quantity; 0 removes. Returns false when the line does not exist.</summary>
    Task<bool> SetQuantityAsync(string buyerId, int productId, int quantity);

    Task<bool> RemoveAsync(string buyerId, int productId);

    Task DeleteAsync(string buyerId);
}

/// <summary>Redis List: capped, most-recent-first, de-duplicated.</summary>
public interface IRecentlyViewedStore
{
    Task AddAsync(string userId, int productId);

    Task<IReadOnlyList<int>> GetAsync(string userId, int take);
}

/// <summary>Redis Set: unique membership, O(1) contains, set algebra (SINTER).</summary>
public interface IWishlistStore
{
    Task<bool> AddAsync(string userId, int productId);

    Task<bool> RemoveAsync(string userId, int productId);

    Task<bool> ContainsAsync(string userId, int productId);

    Task<IReadOnlyList<int>> GetAsync(string userId);

    Task<IReadOnlyList<int>> CommonAsync(string userId, string otherUserId);
}

public sealed record UserSession(string SessionId, string UserId, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, string? UserAgent, string? IpAddress);

/// <summary>Redis Hash per session (sliding TTL) + Set per user (index for "sign out everywhere").</summary>
public interface IUserSessionStore
{
    Task<UserSession> CreateAsync(string userId, string? userAgent, string? ipAddress, TimeSpan idleTimeout);

    Task<UserSession?> GetAndTouchAsync(string sessionId, TimeSpan idleTimeout);

    Task<IReadOnlyList<UserSession>> ListAsync(string userId);

    Task<bool> RevokeAsync(string userId, string sessionId);

    Task<int> RevokeAllAsync(string userId);
}

/// <summary>Redis Bitmap: one bit per user per day.</summary>
public interface IActiveUserTracker
{
    Task TrackAsync(string userId, DateOnly day);

    Task<long> CountAsync(DateOnly endDay, int days);

    Task<bool> WasActiveAsync(string userId, DateOnly day);
}

public sealed record CheckoutEvent(Guid CheckoutId, string BuyerId, IReadOnlyList<CartItem> Items, decimal Total, DateTimeOffset OccurredAt);

/// <summary>Redis Stream producer (XADD).</summary>
public interface ICheckoutEventPublisher
{
    Task<string> PublishAsync(CheckoutEvent checkoutEvent);
}
