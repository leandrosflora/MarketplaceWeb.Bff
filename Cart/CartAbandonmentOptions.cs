namespace MarketplaceWeb.Bff.Cart;

public sealed class CartAbandonmentOptions
{
    public const string SectionName = "Cart";

    public int AbandonmentThresholdMinutes { get; set; } = 60;

    public int ScanIntervalSeconds { get; set; } = 300;
}
