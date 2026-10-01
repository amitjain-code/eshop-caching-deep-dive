using System.Globalization;
using System.Text.Json;
using Basket.Core.Application.Abstractions;
using EShop.Caching.Redis;
using EShop.SharedKernel.Integration;
using StackExchange.Redis;

namespace Basket.Infrastructure.Redis;

/// <summary>
/// <c>XADD eshop:streams:checkout MAXLEN ~ 100000 * checkoutId .. buyerId .. items [..] total .. occurredAt ..</c>
/// The returned entry id (ms-sequence) is time-ordered and unique.
/// </summary>
internal sealed class RedisCheckoutEventPublisher(IRedisStore redis) : ICheckoutEventPublisher
{
    public async Task<string> PublishAsync(CheckoutEvent checkoutEvent)
    {
        ArgumentNullException.ThrowIfNull(checkoutEvent);
        var lines = checkoutEvent.Items.Select(i => new CheckoutLineContract(i.ProductId, i.Quantity, i.UnitPrice)).ToList();

        NameValueEntry[] fields =
        [
            new(CheckoutStream.Fields.CheckoutId, checkoutEvent.CheckoutId.ToString()),
            new(CheckoutStream.Fields.BuyerId, checkoutEvent.BuyerId),
            new(CheckoutStream.Fields.Items, JsonSerializer.Serialize(lines)),
            new(CheckoutStream.Fields.Total, checkoutEvent.Total.ToString(CultureInfo.InvariantCulture)),
            new(CheckoutStream.Fields.OccurredAt, checkoutEvent.OccurredAt.ToString("O", CultureInfo.InvariantCulture)),
        ];

        var id = await redis.SharedDatabase.StreamAddAsync(
            CheckoutStream.Key, fields, messageId: null, maxLength: CheckoutStream.MaxLength, useApproximateMaxLength: true, flags: CommandFlags.None).ConfigureAwait(false);
        return id.ToString();
    }
}
