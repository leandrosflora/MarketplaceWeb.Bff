namespace MarketplaceWeb.Bff.Cart;

public sealed class StoredCart
{
    public List<StoredCartLine> Lines { get; init; } = [];

    public DateTimeOffset LastModifiedAt { get; set; }

    public bool AbandonedFlagSet { get; set; }
}

public sealed record StoredCartLine(
    Guid SkuId,
    Guid SellerId,
    string Title,
    decimal UnitPrice,
    int Quantity);
