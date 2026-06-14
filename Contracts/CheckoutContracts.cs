using MarketplaceWeb.Bff.Clients;

namespace MarketplaceWeb.Bff.Contracts;

public sealed record CreateCheckoutRequest(
    Guid BuyerId,
    AddressDto ShippingAddress,
    IReadOnlyList<CheckoutItemRequest> Items,
    string PaymentMethodId);

public sealed record CheckoutItemRequest(Guid SkuId, int Quantity);

public sealed record ConfirmCheckoutRequest(string PaymentToken, string? PromiseId);

public sealed record CheckoutPageResponse(
    Guid CheckoutId,
    decimal ItemsTotal,
    decimal ShippingPrice,
    decimal TotalAmount,
    string Currency,
    ShippingOptionResponse SelectedShipping,
    IReadOnlyList<CheckoutItemResponse> Items);

public sealed record ShippingOptionResponse(
    string? PromiseId,
    string? Mode,
    string? Carrier,
    DateOnly? EstimatedDeliveryDate,
    decimal Cost);

public sealed record CheckoutItemResponse(Guid SkuId, int Quantity);
