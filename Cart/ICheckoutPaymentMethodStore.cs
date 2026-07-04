namespace MarketplaceWeb.Bff.Cart;

public interface ICheckoutPaymentMethodStore
{
    Task<string?> GetPaymentIntentIdAsync(Guid checkoutId, CancellationToken cancellationToken);

    Task SetPaymentIntentIdAsync(Guid checkoutId, string paymentIntentId, CancellationToken cancellationToken);
}
