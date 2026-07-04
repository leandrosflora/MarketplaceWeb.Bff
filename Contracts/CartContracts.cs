namespace MarketplaceWeb.Bff.Contracts;

public sealed record AddCartItemRequest(
    Guid SkuId,
    Guid SellerId,
    string Title,
    decimal UnitPrice,
    int Quantity);

public sealed record UpdateCartItemQuantityRequest(int Quantity);

public sealed record CartItemResponse(
    Guid SkuId,
    Guid SellerId,
    string Title,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record CartSellerGroupResponse(
    Guid SellerId,
    IReadOnlyList<CartItemResponse> Items,
    decimal Subtotal);

public sealed record CartResponse(
    IReadOnlyList<CartSellerGroupResponse> SellerGroups,
    int TotalItemCount,
    decimal Total,
    bool HasMultipleSellers);

public sealed record ProceedToCheckoutRequest(
    Guid BuyerId,
    string ZipCode);

public sealed record CartCheckoutResponse(IReadOnlyList<Guid> CheckoutIds);

public sealed record PaymentMethodRequest(
    string CardholderName,
    string MaskedCardNumber,
    string ExpiryMonthYear);

public sealed record PaymentMethodResponse(Guid CheckoutId, string PaymentIntentId);
