using MarketplaceWeb.Bff.Application;
using MarketplaceWeb.Bff.Clients;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MarketplaceWeb.Bff.Test.Application;

public class ProductPageComposerTests
{
    private readonly IProductCatalogClient _catalog = Substitute.For<IProductCatalogClient>();
    private readonly IShippingPromiseClient _promise = Substitute.For<IShippingPromiseClient>();
    private readonly ProductPageComposer _sut;

    public ProductPageComposerTests()
    {
        _sut = new ProductPageComposer(_catalog, _promise);
    }

    [Fact]
    public async Task ComposeAsync_ProductNotFound_ReturnsNull()
    {
        _catalog.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProductDto?)null);

        var result = await _sut.ComposeAsync(Guid.NewGuid(), 1, "01310-100", CancellationToken.None);

        Assert.Null(result);
        await _promise.DidNotReceive().CalculateAsync(Arg.Any<ShippingPromiseRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposeAsync_NoZipCode_ReturnsProductWithoutShipping()
    {
        var product = BuildProduct();
        _catalog.GetAsync(product.SkuId, Arg.Any<CancellationToken>()).Returns(product);

        var result = await _sut.ComposeAsync(product.SkuId, 1, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Shipping);
        Assert.Empty(result.Warnings);
        await _promise.DidNotReceive().CalculateAsync(Arg.Any<ShippingPromiseRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposeAsync_EmptyZipCode_ReturnsProductWithoutShipping()
    {
        var product = BuildProduct();
        _catalog.GetAsync(product.SkuId, Arg.Any<CancellationToken>()).Returns(product);

        var result = await _sut.ComposeAsync(product.SkuId, 1, "   ", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Shipping);
        await _promise.DidNotReceive().CalculateAsync(Arg.Any<ShippingPromiseRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposeAsync_WithZipCode_ReturnsProductWithShipping()
    {
        var product = BuildProduct();
        var promise = new ShippingPromiseDto(
            Available: true,
            PromiseId: "prom-1",
            Mode: "STANDARD",
            Carrier: "JADLOG",
            EstimatedDeliveryDate: new DateOnly(2026, 7, 5),
            Cost: 19.90m,
            Source: "calculated",
            UnavailableReason: null);

        _catalog.GetAsync(product.SkuId, Arg.Any<CancellationToken>()).Returns(product);
        _promise.CalculateAsync(Arg.Any<ShippingPromiseRequest>(), Arg.Any<CancellationToken>()).Returns(promise);

        var result = await _sut.ComposeAsync(product.SkuId, 2, "01310-100", CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Shipping);
        Assert.True(result.Shipping.Available);
        Assert.Equal("prom-1", result.Shipping.PromiseId);
        Assert.Equal("STANDARD", result.Shipping.Mode);
        Assert.Equal(new DateOnly(2026, 7, 5), result.Shipping.EstimatedDeliveryDate);
        Assert.Equal(19.90m, result.Shipping.Cost);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ComposeAsync_ShippingPromiseThrows_ReturnsProductWithWarning()
    {
        var product = BuildProduct();
        _catalog.GetAsync(product.SkuId, Arg.Any<CancellationToken>()).Returns(product);
        _promise.CalculateAsync(Arg.Any<ShippingPromiseRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("downstream unavailable"));

        var result = await _sut.ComposeAsync(product.SkuId, 1, "01310-100", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Shipping);
        Assert.Single(result.Warnings);
        Assert.Contains("frete", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("active", true)]
    [InlineData("ACTIVE", true)]
    [InlineData("Inactive", false)]
    [InlineData("suspended", false)]
    public async Task ComposeAsync_MapsAvailableForSaleCaseInsensitive(string status, bool expected)
    {
        var product = BuildProduct() with { Status = status };
        _catalog.GetAsync(product.SkuId, Arg.Any<CancellationToken>()).Returns(product);

        var result = await _sut.ComposeAsync(product.SkuId, 1, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Product.AvailableForSale);
    }

    [Fact]
    public async Task ComposeAsync_MapsProductFieldsCorrectly()
    {
        var product = BuildProduct();
        _catalog.GetAsync(product.SkuId, Arg.Any<CancellationToken>()).Returns(product);

        var result = await _sut.ComposeAsync(product.SkuId, 1, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(product.SkuId, result.Product.SkuId);
        Assert.Equal(product.SellerId, result.Product.SellerId);
        Assert.Equal(product.Title, result.Product.Title);
        Assert.Equal(product.Category, result.Product.Category);
        Assert.Equal(product.Price, result.Product.Price);
    }

    private static ProductDto BuildProduct() => new(
        SkuId: Guid.NewGuid(),
        SellerId: Guid.NewGuid(),
        Title: "Tênis Running X",
        Category: "Calçados",
        Price: 299.90m,
        Status: "Active",
        WeightKg: 0.8m,
        HeightCm: 12m,
        WidthCm: 30m,
        LengthCm: 20m,
        IsFragile: false,
        IsRestricted: false);
}
