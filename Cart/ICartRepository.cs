namespace MarketplaceWeb.Bff.Cart;

public interface ICartRepository
{
    Task<StoredCart?> GetAsync(string cartOwnerId, CancellationToken cancellationToken);

    Task SaveAsync(string cartOwnerId, StoredCart cart, CancellationToken cancellationToken);

    Task DeleteAsync(string cartOwnerId, CancellationToken cancellationToken);

    /// <summary>
    /// Enumerates all currently-stored cart owner ids. Used by the abandonment background scan.
    /// Backed by a Redis set maintained alongside individual cart keys (see RedisCartRepository)
    /// since IDistributedCache alone cannot enumerate keys by prefix.
    /// </summary>
    Task<IReadOnlyCollection<string>> ListOwnerIdsAsync(CancellationToken cancellationToken);
}
