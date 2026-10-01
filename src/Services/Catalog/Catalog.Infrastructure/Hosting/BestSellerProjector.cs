using System.Globalization;
using System.Text.Json;
using Catalog.Core.Application.Analytics;
using EShop.Caching.Redis;
using EShop.SharedKernel.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Catalog.Infrastructure.Hosting;

/// <summary>
/// Redis Streams consumer group: every Catalog replica joins group "catalog-bestsellers"; each entry is
/// delivered to exactly one replica and stays in the Pending Entries List until XACK, so a crash
/// mid-processing does not lose the event. On start we first drain our own pending entries ("0"),
/// then switch to new entries (">").
/// </summary>
internal sealed partial class BestSellerProjector(
    IRedisStore redis,
    IServiceScopeFactory scopeFactory,
    ILogger<BestSellerProjector> logger) : BackgroundService
{
    private const string Group = "catalog-bestsellers";
    private readonly string _consumer = $"{Environment.MachineName}-{Environment.ProcessId}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = redis.SharedDatabase;
        RedisValue position = "0"; // replay own pending entries first

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureGroupAsync(db).ConfigureAwait(false);
                var entries = await db.StreamReadGroupAsync(CheckoutStream.Key, Group, _consumer, position, count: 50, noAck: false, flags: CommandFlags.None).ConfigureAwait(false);

                if (entries.Length == 0)
                {
                    if (position == "0")
                    {
                        position = StreamPosition.NewMessages;
                        continue;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProjectAsync(entry).ConfigureAwait(false);
                    await db.StreamAcknowledgeAsync(CheckoutStream.Key, Group, entry.Id).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RedisException ex)
            {
                LogRedisError(logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task EnsureGroupAsync(IDatabase db)
    {
        try
        {
            await db.StreamCreateConsumerGroupAsync(CheckoutStream.Key, Group, StreamPosition.Beginning, createStream: true, flags: CommandFlags.None).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
        {
            // group already exists
        }
    }

    private async Task ProjectAsync(StreamEntry entry)
    {
        try
        {
            var lines = JsonSerializer.Deserialize<List<CheckoutLineContract>>(entry[CheckoutStream.Fields.Items].ToString()) ?? [];
            var occurredAt = DateTimeOffset.Parse(entry[CheckoutStream.Fields.OccurredAt].ToString(), CultureInfo.InvariantCulture);

            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ProjectCheckoutToBestSellersHandler>()
                .HandleAsync(lines, occurredAt).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            // Poison message: log and ack so it does not block the group. Real systems dead-letter it.
            LogPoison(logger, ex, entry.Id.ToString());
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis error while consuming checkout stream")]
    private static partial void LogRedisError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Skipping malformed checkout event {EntryId}")]
    private static partial void LogPoison(ILogger logger, Exception exception, string entryId);
}
