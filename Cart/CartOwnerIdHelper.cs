namespace MarketplaceWeb.Bff.Cart;

/// <summary>
/// `cartOwnerId` is an opaque string supplied by `MarketplaceWeb` on every cart call: either the
/// authenticated buyer's `BuyerId` claim value (a raw GUID string) or an anonymous cart id
/// prefixed with "anon:" (a cookie-issued GUID). This lets the BFF tell the two apart without
/// needing its own session/auth state.
/// </summary>
public static class CartOwnerIdHelper
{
    public const string AnonymousPrefix = "anon:";

    public static bool TryGetBuyerId(string cartOwnerId, out Guid buyerId)
    {
        if (cartOwnerId.StartsWith(AnonymousPrefix, StringComparison.Ordinal))
        {
            buyerId = Guid.Empty;
            return false;
        }

        return Guid.TryParse(cartOwnerId, out buyerId);
    }
}
