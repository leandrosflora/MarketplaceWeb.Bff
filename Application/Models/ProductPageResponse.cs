namespace MarketplaceWeb.Bff.Application.Models;

public sealed record ProductPageResponse(
    ProductSummary Product,
    ShippingSummary? Shipping,
    IReadOnlyList<string> Warnings);

public sealed record ProductSummary(
    Guid SkuId,
    Guid SellerId,
    string Title,
    string Category,
    decimal Price,
    bool AvailableForSale,
    string? ImageUrl);

public sealed record ShippingSummary(
    bool Available,
    string? PromiseId,
    string? Mode,
    DateOnly? EstimatedDeliveryDate,
    decimal? Cost,
    string? UnavailableReason);
