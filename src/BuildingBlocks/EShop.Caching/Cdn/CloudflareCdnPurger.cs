using System.Net.Http.Headers;
using System.Net.Http.Json;
using EShop.Caching.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EShop.Caching.Cdn;

/// <summary>Cloudflare purge-by-tag: <c>POST /zones/{zone}/purge_cache {"tags":[...]}</c> (max 30 tags per call).</summary>
internal sealed partial class CloudflareCdnPurger(
    HttpClient httpClient,
    IOptions<CachingOptions> options,
    ILogger<CloudflareCdnPurger> logger) : ICdnPurger
{
    private const int MaxTagsPerRequest = 30;

    public async Task PurgeTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        var cdn = options.Value.Cdn;
        if (string.IsNullOrWhiteSpace(cdn.ZoneId) || string.IsNullOrWhiteSpace(cdn.ApiToken))
        {
            LogNotConfigured(logger);
            return;
        }

        foreach (var batch in tags.Chunk(MaxTagsPerRequest))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"zones/{cdn.ZoneId}/purge_cache")
            {
                Content = JsonContent.Create(new { tags = batch }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cdn.ApiToken);

            // A failed purge must not fail the business write: content still expires via s-maxage.
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    LogPurgeFailed(logger, (int)response.StatusCode, string.Join(',', batch));
                }
            }
            catch (HttpRequestException ex)
            {
                LogPurgeError(logger, ex, string.Join(',', batch));
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cloudflare purge skipped: Caching:Cdn:ZoneId/ApiToken not configured")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cloudflare purge returned {StatusCode} for tags {Tags}")]
    private static partial void LogPurgeFailed(ILogger logger, int statusCode, string tags);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cloudflare purge failed for tags {Tags}")]
    private static partial void LogPurgeError(ILogger logger, Exception exception, string tags);
}
