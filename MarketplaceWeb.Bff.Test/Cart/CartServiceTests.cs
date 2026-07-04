using MarketplaceWeb.Bff.Cart;
using MarketplaceWeb.Bff.Clients;
using MarketplaceWeb.Bff.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MarketplaceWeb.Bff.Test.Cart;

public sealed class CartServiceTests
{
    private readonly InMemoryCartRepository _repository = new();
    private readonly ICheckoutClient _checkoutClient = Substitute.For<ICheckoutClient>();
    private readonly CartService _sut;

    public CartServiceTests()
    {
        _sut = new CartService(_repository, _checkoutClient, NullLogger<CartService>.Instance);
    }

    [Fact]
    public async Task AddItemAsync_NewSku_AddsLine()
    {
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        var result = await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(skuId, sellerId, "Produto", 10m, 2), CancellationToken.None);

        var item = Assert.Single(Assert.Single(result.SellerGroups).Items);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(20m, item.LineTotal);
    }

    [Fact]
    public async Task AddItemAsync_ExistingSku_IncrementsQuantityInsteadOfDuplicating()
    {
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(skuId, sellerId, "Produto", 10m, 1), CancellationToken.None);

        var result = await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(skuId, sellerId, "Produto", 10m, 2), CancellationToken.None);

        var group = Assert.Single(result.SellerGroups);
        var item = Assert.Single(group.Items);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public async Task UpdateQuantityAsync_ToZero_RemovesLine()
    {
        var skuId = Guid.NewGuid();
        await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(skuId, Guid.NewGuid(), "Produto", 10m, 1), CancellationToken.None);

        var result = await _sut.UpdateQuantityAsync("buyer-1", skuId, 0, CancellationToken.None);

        Assert.Empty(result.SellerGroups);
        Assert.Equal(0, result.TotalItemCount);
    }

    [Fact]
    public async Task GetAsync_MultipleSellers_GroupsBySellerAndFlagsMultiSeller()
    {
        var sellerA = Guid.NewGuid();
        var sellerB = Guid.NewGuid();
        await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(Guid.NewGuid(), sellerA, "A", 10m, 1), CancellationToken.None);
        await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(Guid.NewGuid(), sellerB, "B", 20m, 1), CancellationToken.None);

        var result = await _sut.GetAsync("buyer-1", CancellationToken.None);

        Assert.Equal(2, result.SellerGroups.Count);
        Assert.True(result.HasMultipleSellers);
        Assert.Equal(30m, result.Total);
    }

    [Fact]
    public async Task MergeAnonymousCartAsync_NoPriorBuyerCart_CopiesAnonymousItems()
    {
        var skuId = Guid.NewGuid();
        await _sut.AddItemAsync("anon:123", new AddCartItemRequest(skuId, Guid.NewGuid(), "Produto", 10m, 2), CancellationToken.None);

        await _sut.MergeAnonymousCartAsync("anon:123", "buyer-1", CancellationToken.None);

        var buyerCart = await _sut.GetAsync("buyer-1", CancellationToken.None);
        var item = Assert.Single(Assert.Single(buyerCart.SellerGroups).Items);
        Assert.Equal(skuId, item.SkuId);
        Assert.Equal(2, item.Quantity);

        var anonymousCartAfterMerge = await _repository.GetAsync("anon:123", CancellationToken.None);
        Assert.Null(anonymousCartAfterMerge);
    }

    [Fact]
    public async Task MergeAnonymousCartAsync_OverlappingSku_SumsQuantities()
    {
        var sharedSku = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(sharedSku, sellerId, "Produto", 10m, 1), CancellationToken.None);
        await _sut.AddItemAsync("anon:123", new AddCartItemRequest(sharedSku, sellerId, "Produto", 10m, 2), CancellationToken.None);
        var otherSku = Guid.NewGuid();
        await _sut.AddItemAsync("anon:123", new AddCartItemRequest(otherSku, sellerId, "Outro", 5m, 1), CancellationToken.None);

        await _sut.MergeAnonymousCartAsync("anon:123", "buyer-1", CancellationToken.None);

        var buyerCart = await _sut.GetAsync("buyer-1", CancellationToken.None);
        var group = Assert.Single(buyerCart.SellerGroups);
        Assert.Equal(2, group.Items.Count);
        Assert.Equal(3, group.Items.Single(i => i.SkuId == sharedSku).Quantity);
        Assert.Equal(1, group.Items.Single(i => i.SkuId == otherSku).Quantity);
    }

    [Fact]
    public async Task ProceedToCheckoutAsync_MultipleSellers_CreatesOneCheckoutPerSeller()
    {
        var sellerA = Guid.NewGuid();
        var sellerB = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(Guid.NewGuid(), sellerA, "A", 10m, 1), CancellationToken.None);
        await _sut.AddItemAsync("buyer-1", new AddCartItemRequest(Guid.NewGuid(), sellerB, "B", 20m, 1), CancellationToken.None);

        _checkoutClient.CreateAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<CreateCheckoutRequest>();
                return Task.FromResult(new CheckoutPageResponse(Guid.NewGuid(), 0, 0, 0, "BRL", new ShippingOptionResponse(null, null, null, null, 0), []));
            });

        var result = await _sut.ProceedToCheckoutAsync("buyer-1", new ProceedToCheckoutRequest(buyerId, "01310100"), CancellationToken.None);

        Assert.Equal(2, result.CheckoutIds.Count);
        await _checkoutClient.Received(2).CreateAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var cartAfterCheckout = await _repository.GetAsync("buyer-1", CancellationToken.None);
        Assert.Null(cartAfterCheckout);
    }

    [Fact]
    public async Task ProceedToCheckoutAsync_EmptyCart_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ProceedToCheckoutAsync("buyer-empty", new ProceedToCheckoutRequest(Guid.NewGuid(), "01310100"), CancellationToken.None));
    }

    private sealed class InMemoryCartRepository : ICartRepository
    {
        private readonly Dictionary<string, StoredCart> _carts = new();

        public Task<StoredCart?> GetAsync(string cartOwnerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_carts.TryGetValue(cartOwnerId, out var cart) ? cart : null);
        }

        public Task SaveAsync(string cartOwnerId, StoredCart cart, CancellationToken cancellationToken)
        {
            _carts[cartOwnerId] = cart;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string cartOwnerId, CancellationToken cancellationToken)
        {
            _carts.Remove(cartOwnerId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> ListOwnerIdsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<string>>(_carts.Keys.ToArray());
        }
    }
}
