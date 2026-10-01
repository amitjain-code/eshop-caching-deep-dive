using Microsoft.Extensions.Logging;

namespace EShop.Caching.Cdn;

/// <summary>Used locally (the nginx edge in docker-compose relies on short s-maxage instead of purges).</summary>
internal sealed partial class NoOpCdnPurger(ILogger<NoOpCdnPurger> logger) : ICdnPurger
{
    public Task PurgeTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        LogSkipped(logger, string.Join(',', tags));
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "CDN purge skipped (no provider configured) for tags {Tags}")]
    private static partial void LogSkipped(ILogger logger, string tags);
}
