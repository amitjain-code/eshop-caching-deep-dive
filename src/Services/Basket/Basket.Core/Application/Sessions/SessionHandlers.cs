using Basket.Core.Application.Abstractions;
using Basket.Core.Domain;
using EShop.SharedKernel;

namespace Basket.Core.Application.Sessions;

/// <summary>
/// Server-side sessions shared by every replica. Sticky sessions are unnecessary because any node can
/// read the session Hash; revoking a session takes effect immediately everywhere (unlike a JWT).
/// </summary>
public sealed class SessionHandlers(IUserSessionStore store)
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    public Task<UserSession> CreateAsync(string userId, string? userAgent, string? ipAddress) =>
        store.CreateAsync(userId, userAgent, ipAddress, IdleTimeout);

    public async Task<Result<UserSession>> ValidateAsync(string sessionId)
    {
        var session = await store.GetAndTouchAsync(sessionId, IdleTimeout).ConfigureAwait(false);
        if (session is null)
        {
            return BasketErrors.SessionNotFound;
        }

        return session;
    }

    public Task<IReadOnlyList<UserSession>> ListAsync(string userId) => store.ListAsync(userId);

    public Task<bool> RevokeAsync(string userId, string sessionId) => store.RevokeAsync(userId, sessionId);

    public Task<int> RevokeAllAsync(string userId) => store.RevokeAllAsync(userId);
}
