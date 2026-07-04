using MarketplaceWeb.Bff.Cart;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace MarketplaceWeb.Bff.Test.Cart;

public sealed class CartAbandonmentScannerTests
{
    private readonly ICartRepository _repository = Substitute.For<ICartRepository>();
    private readonly ICartEventPublisher _publisher = Substitute.For<ICartEventPublisher>();
    private readonly CartAbandonmentScanner _sut;

    public CartAbandonmentScannerTests()
    {
        var options = Options.Create(new CartAbandonmentOptions { AbandonmentThresholdMinutes = 60 });
        _sut = new CartAbandonmentScanner(_repository, _publisher, options, NullLogger<CartAbandonmentScanner>.Instance);
    }

    [Fact]
    public async Task ScanAsync_CartInactiveJustOverThreshold_FlagsAndPublishes()
    {
        var cart = new StoredCart
        {
            Lines = { new StoredCartLine(Guid.NewGuid(), Guid.NewGuid(), "Produto", 10m, 1) },
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-61)
        };
        _repository.ListOwnerIdsAsync(Arg.Any<CancellationToken>()).Returns(new[] { "buyer-1" });
        _repository.GetAsync("buyer-1", Arg.Any<CancellationToken>()).Returns(cart);

        await _sut.ScanAsync(CancellationToken.None);

        await _publisher.Received(1).PublishAbandonedAsync("buyer-1", cart, Arg.Any<CancellationToken>());
        Assert.True(cart.AbandonedFlagSet);
        await _repository.Received(1).SaveAsync("buyer-1", cart, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_CartActiveJustUnderThreshold_DoesNotFlag()
    {
        var cart = new StoredCart
        {
            Lines = { new StoredCartLine(Guid.NewGuid(), Guid.NewGuid(), "Produto", 10m, 1) },
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-59)
        };
        _repository.ListOwnerIdsAsync(Arg.Any<CancellationToken>()).Returns(new[] { "buyer-1" });
        _repository.GetAsync("buyer-1", Arg.Any<CancellationToken>()).Returns(cart);

        await _sut.ScanAsync(CancellationToken.None);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAbandonedAsync(default!, default!, default);
        Assert.False(cart.AbandonedFlagSet);
    }

    [Fact]
    public async Task ScanAsync_AlreadyFlaggedCart_DoesNotPublishAgain()
    {
        var cart = new StoredCart
        {
            Lines = { new StoredCartLine(Guid.NewGuid(), Guid.NewGuid(), "Produto", 10m, 1) },
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-120),
            AbandonedFlagSet = true
        };
        _repository.ListOwnerIdsAsync(Arg.Any<CancellationToken>()).Returns(new[] { "buyer-1" });
        _repository.GetAsync("buyer-1", Arg.Any<CancellationToken>()).Returns(cart);

        await _sut.ScanAsync(CancellationToken.None);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAbandonedAsync(default!, default!, default);
    }

    [Fact]
    public async Task ScanAsync_EmptyCart_IsSkipped()
    {
        var cart = new StoredCart { LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-120) };
        _repository.ListOwnerIdsAsync(Arg.Any<CancellationToken>()).Returns(new[] { "buyer-1" });
        _repository.GetAsync("buyer-1", Arg.Any<CancellationToken>()).Returns(cart);

        await _sut.ScanAsync(CancellationToken.None);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAbandonedAsync(default!, default!, default);
    }
}
