using Microsoft.Extensions.Caching.Distributed;

namespace MarketplaceWeb.Bff.Cart;

public sealed class RedisCheckoutPaymentMethodStore : ICheckoutPaymentMethodStore
{
    private readonly IDistributedCache _cache;

    public RedisCheckoutPaymentMethodStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public Task<string?> GetPaymentIntentIdAsync(Guid checkoutId, CancellationToken cancellationToken)
    {
        return _cache.GetStringAsync(BuildKey(checkoutId), cancellationToken);
    }

    public Task SetPaymentIntentIdAsync(Guid checkoutId, string paymentIntentId, CancellationToken cancellationToken)
    {
        return _cache.SetStringAsync(
            BuildKey(checkoutId),
            paymentIntentId,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
            },
            cancellationToken);
    }

    private static string BuildKey(Guid checkoutId) => $"checkout-payment:{checkoutId:N}";
}
