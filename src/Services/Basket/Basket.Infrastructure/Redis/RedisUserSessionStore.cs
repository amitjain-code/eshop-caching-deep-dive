using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using Basket.Core.Application.Abstractions;
using EShop.Caching.Redis;
using StackExchange.Redis;

namespace Basket.Infrastructure.Redis;

/// <summary>
/// Shared sessions with <b>Hash + Set</b>:
/// <code>
/// HSET session:{id} userId alice createdAt .. lastSeenAt .. ua .. ip ..   + EXPIRE 30m (sliding)
/// SADD user-sessions:alice {id}                                           index for list / revoke-all
/// </code>
/// Hash fields let us update lastSeenAt alone (HSET one field) instead of rewriting a JSON blob.
/// </summary>
internal sealed class RedisUserSessionStore(IRedisStore redis, TimeProvider clock) : IUserSessionStore
{
    public async Task<UserSession> CreateAsync(string userId, string? userAgent, string? ipAddress, TimeSpan idleTimeout)
    {
        var now = clock.GetUtcNow();
        var session = new UserSession(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)), userId, now, now, userAgent, ipAddress);

        var transaction = redis.Database.CreateTransaction();
        _ = transaction.HashSetAsync(SessionKey(session.SessionId),
        [
            new HashEntry("userId", userId),
            new HashEntry("createdAt", now.ToString("O", CultureInfo.InvariantCulture)),
            new HashEntry("lastSeenAt", now.ToString("O", CultureInfo.InvariantCulture)),
            new HashEntry("userAgent", userAgent ?? string.Empty),
            new HashEntry("ip", ipAddress ?? string.Empty),
        ]);
        _ = transaction.KeyExpireAsync(SessionKey(session.SessionId), idleTimeout);
        _ = transaction.SetAddAsync(UserIndexKey(userId), session.SessionId);
        await transaction.ExecuteAsync().ConfigureAwait(false);

        return session;
    }

    public async Task<UserSession?> GetAndTouchAsync(string sessionId, TimeSpan idleTimeout)
    {
        var key = SessionKey(sessionId);
        var session = Map(sessionId, await redis.Database.HashGetAllAsync(key).ConfigureAwait(false));
        if (session is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var batch = redis.Database.CreateBatch();
        var touch = batch.HashSetAsync(key, "lastSeenAt", now.ToString("O", CultureInfo.InvariantCulture));
        var slide = batch.KeyExpireAsync(key, idleTimeout); // sliding expiration
        batch.Execute();
        await Task.WhenAll(touch, slide).ConfigureAwait(false);

        return session with { LastSeenAt = now };
    }

    public async Task<IReadOnlyList<UserSession>> ListAsync(string userId)
    {
        var ids = await redis.Database.SetMembersAsync(UserIndexKey(userId)).ConfigureAwait(false);
        var batch = redis.Database.CreateBatch();
        var reads = ids.Select(id => (Id: id.ToString(), Task: batch.HashGetAllAsync(SessionKey(id.ToString())))).ToList();
        batch.Execute();
        await Task.WhenAll(reads.Select(r => r.Task)).ConfigureAwait(false);

        var sessions = new List<UserSession>();
        var expired = new List<RedisValue>();
        foreach (var (id, task) in reads)
        {
            var session = Map(id, await task.ConfigureAwait(false));
            if (session is null)
            {
                expired.Add(id); // the Hash expired; lazily clean the index
            }
            else
            {
                sessions.Add(session);
            }
        }

        if (expired.Count > 0)
        {
            await redis.Database.SetRemoveAsync(UserIndexKey(userId), expired.ToArray()).ConfigureAwait(false);
        }

        return [.. sessions.OrderByDescending(s => s.LastSeenAt)];
    }

    public async Task<bool> RevokeAsync(string userId, string sessionId)
    {
        var owner = await redis.Database.HashGetAsync(SessionKey(sessionId), "userId").ConfigureAwait(false);
        if (owner.IsNull || owner != userId)
        {
            return false;
        }

        var transaction = redis.Database.CreateTransaction();
        _ = transaction.KeyDeleteAsync(SessionKey(sessionId));
        _ = transaction.SetRemoveAsync(UserIndexKey(userId), sessionId);
        return await transaction.ExecuteAsync().ConfigureAwait(false);
    }

    public async Task<int> RevokeAllAsync(string userId)
    {
        var ids = await redis.Database.SetMembersAsync(UserIndexKey(userId)).ConfigureAwait(false);
        var keys = ids.Select(id => SessionKey(id.ToString())).Append(UserIndexKey(userId)).ToArray();
        await redis.Database.KeyDeleteAsync(keys).ConfigureAwait(false);
        return ids.Length;
    }

    private static UserSession? Map(string sessionId, HashEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return null;
        }

        var map = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString(), StringComparer.Ordinal);
        return new UserSession(
            sessionId,
            map.GetValueOrDefault("userId", string.Empty),
            DateTimeOffset.Parse(map.GetValueOrDefault("createdAt", "1970-01-01T00:00:00Z"), CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(map.GetValueOrDefault("lastSeenAt", "1970-01-01T00:00:00Z"), CultureInfo.InvariantCulture),
            NullIfEmpty(map.GetValueOrDefault("userAgent")),
            NullIfEmpty(map.GetValueOrDefault("ip")));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static RedisKey SessionKey(string sessionId) => $"session:{sessionId}";

    private static RedisKey UserIndexKey(string userId) => $"user-sessions:{userId}";
}
