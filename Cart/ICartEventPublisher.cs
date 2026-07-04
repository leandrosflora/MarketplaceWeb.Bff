namespace MarketplaceWeb.Bff.Cart;

public interface ICartEventPublisher
{
    Task PublishAbandonedAsync(string cartOwnerId, StoredCart cart, CancellationToken cancellationToken);
}
