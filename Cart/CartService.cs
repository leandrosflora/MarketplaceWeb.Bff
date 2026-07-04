using MarketplaceWeb.Bff.Clients;
using MarketplaceWeb.Bff.Contracts;

namespace MarketplaceWeb.Bff.Cart;

public sealed class CartService
{
    private readonly ICartRepository _repository;
    private readonly ICheckoutClient _checkoutClient;
    private readonly ILogger<CartService> _logger;

    public CartService(ICartRepository repository, ICheckoutClient checkoutClient, ILogger<CartService> logger)
    {
        _repository = repository;
        _checkoutClient = checkoutClient;
        _logger = logger;
    }

    public async Task<CartResponse> GetAsync(string cartOwnerId, CancellationToken cancellationToken)
    {
        var cart = await _repository.GetAsync(cartOwnerId, cancellationToken);

        return ToResponse(cart);
    }

    public async Task<CartResponse> AddItemAsync(string cartOwnerId, AddCartItemRequest request, CancellationToken cancellationToken)
    {
        var cart = await _repository.GetAsync(cartOwnerId, cancellationToken) ?? new StoredCart();

        var existingIndex = cart.Lines.FindIndex(line => line.SkuId == request.SkuId);

        if (existingIndex >= 0)
        {
            var existing = cart.Lines[existingIndex];
            cart.Lines[existingIndex] = existing with { Quantity = existing.Quantity + request.Quantity };
        }
        else
        {
            cart.Lines.Add(new StoredCartLine(request.SkuId, request.SellerId, request.Title, request.UnitPrice, request.Quantity));
        }

        cart.LastModifiedAt = DateTimeOffset.UtcNow;
        cart.AbandonedFlagSet = false;

        await _repository.SaveAsync(cartOwnerId, cart, cancellationToken);

        return ToResponse(cart);
    }

    public async Task<CartResponse> UpdateQuantityAsync(string cartOwnerId, Guid skuId, int quantity, CancellationToken cancellationToken)
    {
        var cart = await _repository.GetAsync(cartOwnerId, cancellationToken) ?? new StoredCart();

        if (quantity <= 0)
        {
            cart.Lines.RemoveAll(line => line.SkuId == skuId);
        }
        else
        {
            var index = cart.Lines.FindIndex(line => line.SkuId == skuId);

            if (index >= 0)
            {
                cart.Lines[index] = cart.Lines[index] with { Quantity = quantity };
            }
        }

        cart.LastModifiedAt = DateTimeOffset.UtcNow;
        cart.AbandonedFlagSet = false;

        if (cart.Lines.Count == 0)
        {
            await _repository.SaveAsync(cartOwnerId, cart, cancellationToken);
            return ToResponse(cart);
        }

        await _repository.SaveAsync(cartOwnerId, cart, cancellationToken);

        return ToResponse(cart);
    }

    public async Task<CartResponse> RemoveItemAsync(string cartOwnerId, Guid skuId, CancellationToken cancellationToken)
    {
        return await UpdateQuantityAsync(cartOwnerId, skuId, 0, cancellationToken);
    }

    /// <summary>
    /// Additively merges an anonymous cart into the authenticated buyer's cart (summing quantities
    /// for shared SKUs, appending others), then deletes the anonymous cart. Never overwrites the
    /// buyer's existing lines outright, so a buggy call can't silently drop items.
    /// </summary>
    public async Task MergeAnonymousCartAsync(string anonymousCartOwnerId, string buyerCartOwnerId, CancellationToken cancellationToken)
    {
        if (string.Equals(anonymousCartOwnerId, buyerCartOwnerId, StringComparison.Ordinal))
        {
            return;
        }

        var anonymousCart = await _repository.GetAsync(anonymousCartOwnerId, cancellationToken);

        if (anonymousCart is null || anonymousCart.Lines.Count == 0)
        {
            return;
        }

        var buyerCart = await _repository.GetAsync(buyerCartOwnerId, cancellationToken) ?? new StoredCart();
        var buyerItemCountBefore = buyerCart.Lines.Sum(line => line.Quantity);

        foreach (var anonymousLine in anonymousCart.Lines)
        {
            var index = buyerCart.Lines.FindIndex(line => line.SkuId == anonymousLine.SkuId);

            if (index >= 0)
            {
                var existing = buyerCart.Lines[index];
                buyerCart.Lines[index] = existing with { Quantity = existing.Quantity + anonymousLine.Quantity };
            }
            else
            {
                buyerCart.Lines.Add(anonymousLine);
            }
        }

        buyerCart.LastModifiedAt = DateTimeOffset.UtcNow;
        buyerCart.AbandonedFlagSet = false;

        await _repository.SaveAsync(buyerCartOwnerId, buyerCart, cancellationToken);
        await _repository.DeleteAsync(anonymousCartOwnerId, cancellationToken);

        _logger.LogInformation(
            "Merged anonymous cart {AnonymousCartOwnerId} into buyer cart {BuyerCartOwnerId}: {ItemsBefore} item(s) before merge, {ItemsAfter} item(s) after",
            anonymousCartOwnerId,
            buyerCartOwnerId,
            buyerItemCountBefore,
            buyerCart.Lines.Sum(line => line.Quantity));
    }

    public async Task<CartCheckoutResponse> ProceedToCheckoutAsync(string cartOwnerId, ProceedToCheckoutRequest request, CancellationToken cancellationToken)
    {
        var cart = await _repository.GetAsync(cartOwnerId, cancellationToken);

        if (cart is null || cart.Lines.Count == 0)
        {
            throw new InvalidOperationException("Cart is empty; nothing to check out.");
        }

        var checkoutIds = new List<Guid>();

        foreach (var sellerGroup in cart.Lines.GroupBy(line => line.SellerId))
        {
            var checkoutRequest = new CreateCheckoutRequest(
                request.BuyerId,
                sellerGroup.Key,
                new AddressDto(request.ZipCode, string.Empty, string.Empty, "BR"),
                sellerGroup.Select(line => new CheckoutItemRequest(line.SkuId, line.Quantity, line.UnitPrice)).ToList());

            var checkout = await _checkoutClient.CreateAsync(checkoutRequest, Guid.NewGuid().ToString("N"), cancellationToken);
            checkoutIds.Add(checkout.CheckoutId);
        }

        await _repository.DeleteAsync(cartOwnerId, cancellationToken);

        return new CartCheckoutResponse(checkoutIds);
    }

    private static CartResponse ToResponse(StoredCart? cart)
    {
        if (cart is null || cart.Lines.Count == 0)
        {
            return new CartResponse([], 0, 0m, false);
        }

        var sellerGroups = cart.Lines
            .GroupBy(line => line.SellerId)
            .Select(group =>
            {
                var items = group
                    .Select(line => new CartItemResponse(line.SkuId, line.SellerId, line.Title, line.UnitPrice, line.Quantity, line.UnitPrice * line.Quantity))
                    .ToList();

                return new CartSellerGroupResponse(group.Key, items, items.Sum(item => item.LineTotal));
            })
            .ToList();

        return new CartResponse(
            sellerGroups,
            cart.Lines.Sum(line => line.Quantity),
            sellerGroups.Sum(group => group.Subtotal),
            sellerGroups.Count > 1);
    }
}
