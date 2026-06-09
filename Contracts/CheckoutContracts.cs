using MarketplaceWeb.Bff.Clients;

namespace MarketplaceWeb.Bff.Contracts;

public sealed record CreateCheckoutRequest(
    Guid BuyerId,
    AddressDto ShippingAddress,
    IReadOnlyList<CheckoutItemRequest> Items,
    string PaymentMethodId);

public sealed record CheckoutItemRequest(Guid SkuId, int Quantity);

public sealed record ConfirmCheckoutRequest(string PaymentToken, string? PromiseId);

public sealed record CheckoutResponse(
    Guid CheckoutId,
    string Status,
    decimal ItemsTotal,
    decimal ShippingPrice,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset ExpiresAt);
