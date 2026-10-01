using Basket.Core.Application.Abstractions;

namespace Basket.Core.Application.Analytics;

public sealed record ActiveUsersDto(DateOnly EndDay, int Days, long ActiveUsers);

/// <summary>DAU/WAU/MAU from daily bitmaps: BITCOUNT for one day, BITOP OR + BITCOUNT for a range.</summary>
public sealed class ActiveUsersHandler(IActiveUserTracker tracker, TimeProvider clock)
{
    public Task TrackAsync(string userId) => tracker.TrackAsync(userId, Today());

    public async Task<ActiveUsersDto> CountAsync(int days)
    {
        days = Math.Clamp(days, 1, 31);
        var today = Today();
        return new ActiveUsersDto(today, days, await tracker.CountAsync(today, days).ConfigureAwait(false));
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
