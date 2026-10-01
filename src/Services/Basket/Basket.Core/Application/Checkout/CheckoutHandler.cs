using Basket.Core.Application.Abstractions;
using Basket.Core.Domain;
using EShop.Caching;
using EShop.SharedKernel;

namespace Basket.Core.Application.Checkout;

public sealed record CheckoutResult(Guid CheckoutId, decimal Total, string StreamEntryId);

/// <summary>
/// 1. distributed lock (String SET NX PX) stops double-submits from two tabs / two replicas
/// 2. publish to a Redis Stream (durable, replayable, consumer groups)
/// 3. delete the cart Hash
/// </summary>
public sealed class CheckoutHandler(ICartRepository carts, IDistributedLock locks, ICheckoutEventPublisher publisher, TimeProvider clock)
{
    private static readonly TimeSpan LockLease = TimeSpan.FromSeconds(30);

    public async Task<Result<CheckoutResult>> HandleAsync(string buyerId)
    {
        var lease = await locks.TryAcquireAsync($"checkout:{buyerId}", LockLease).ConfigureAwait(false);
        if (lease is null)
        {
            return BasketErrors.CheckoutInProgress;
        }

        await using (lease.ConfigureAwait(false))
        {
            var cart = await carts.GetAsync(buyerId).ConfigureAwait(false);
            if (cart.IsEmpty)
            {
                return BasketErrors.EmptyCart;
            }

            var checkout = new CheckoutEvent(Guid.CreateVersion7(), buyerId, cart.Items, cart.Total, clock.GetUtcNow());
            var entryId = await publisher.PublishAsync(checkout).ConfigureAwait(false);
            await carts.DeleteAsync(buyerId).ConfigureAwait(false);

            return new CheckoutResult(checkout.CheckoutId, checkout.Total, entryId);
        }
    }
}
