using MarketplaceWeb.Bff.Application;
using MarketplaceWeb.Bff.Clients;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MarketplaceWeb.Bff.Test.Application;

public class OrderPageComposerTests
{
    private readonly IOrderClient _orders = Substitute.For<IOrderClient>();
    private readonly IShipmentClient _shipments = Substitute.For<IShipmentClient>();
    private readonly ITrackingClient _tracking = Substitute.For<ITrackingClient>();
    private readonly OrderPageComposer _sut;

    public OrderPageComposerTests()
    {
        _sut = new OrderPageComposer(_orders, _shipments, _tracking);
    }

    [Fact]
    public async Task ComposeAsync_OrderNotFound_ReturnsNull()
    {
        _orders.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((OrderDto?)null);

        var result = await _sut.ComposeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
        await _shipments.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _tracking.DidNotReceive().GetByShipmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposeAsync_OrderWithoutShipment_ReturnsOrderWithNoShipmentOrTracking()
    {
        var order = BuildOrder(shipmentId: null);
        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Shipment);
        Assert.Null(result.Tracking);
        Assert.Empty(result.Warnings);
        await _shipments.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _tracking.DidNotReceive().GetByShipmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposeAsync_OrderWithShipment_ReturnsFullResponse()
    {
        var shipmentId = Guid.NewGuid();
        var order = BuildOrder(shipmentId);
        var shipment = BuildShipment(shipmentId);
        var trackingDto = BuildTracking();

        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _shipments.GetAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(shipment);
        _tracking.GetByShipmentAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(trackingDto);

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Shipment);
        Assert.NotNull(result.Tracking);
        Assert.Empty(result.Warnings);

        Assert.Equal(shipment.Id, result.Shipment.ShipmentId);
        Assert.Equal(shipment.Status, result.Shipment.Status);
        Assert.Equal(shipment.CarrierCode, result.Shipment.CarrierCode);
        Assert.Equal(shipment.TrackingCode, result.Shipment.TrackingCode);
        Assert.Equal(shipment.PromisedDeliveryDate, result.Shipment.PromisedDeliveryDate);
    }

    [Fact]
    public async Task ComposeAsync_ShipmentNotFound_AddsWarningAndReturnsNullShipment()
    {
        var shipmentId = Guid.NewGuid();
        var order = BuildOrder(shipmentId);
        var trackingDto = BuildTracking();

        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _shipments.GetAsync(shipmentId, Arg.Any<CancellationToken>()).Returns((ShipmentDto?)null);
        _tracking.GetByShipmentAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(trackingDto);

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Shipment);
        Assert.Single(result.Warnings);
        Assert.Contains("entrega", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComposeAsync_TrackingNotFound_AddsWarningAndReturnsNullTracking()
    {
        var shipmentId = Guid.NewGuid();
        var order = BuildOrder(shipmentId);
        var shipment = BuildShipment(shipmentId);

        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _shipments.GetAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(shipment);
        _tracking.GetByShipmentAsync(shipmentId, Arg.Any<CancellationToken>()).Returns((TrackingDto?)null);

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Shipment);
        Assert.Null(result.Tracking);
        Assert.Single(result.Warnings);
        Assert.Contains("rastreamento", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComposeAsync_ShipmentThrows_AddsWarningAndReturnsNullShipment()
    {
        var shipmentId = Guid.NewGuid();
        var order = BuildOrder(shipmentId);
        var trackingDto = BuildTracking();

        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _shipments.GetAsync(shipmentId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("timeout"));
        _tracking.GetByShipmentAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(trackingDto);

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Shipment);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task ComposeAsync_TrackingThrows_AddsWarningAndReturnsNullTracking()
    {
        var shipmentId = Guid.NewGuid();
        var order = BuildOrder(shipmentId);
        var shipment = BuildShipment(shipmentId);

        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _shipments.GetAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(shipment);
        _tracking.GetByShipmentAsync(shipmentId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("timeout"));

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Shipment);
        Assert.Null(result.Tracking);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task ComposeAsync_BothDownstreamsThrow_AddsTwoWarnings()
    {
        var shipmentId = Guid.NewGuid();
        var order = BuildOrder(shipmentId);

        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _shipments.GetAsync(shipmentId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("timeout"));
        _tracking.GetByShipmentAsync(shipmentId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("timeout"));

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Shipment);
        Assert.Null(result.Tracking);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public async Task ComposeAsync_MapsOrderFieldsCorrectly()
    {
        var order = BuildOrder(shipmentId: null);
        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(order.Id, result.Order.OrderId);
        Assert.Equal(order.Status, result.Order.Status);
        Assert.Equal(order.ItemsTotal, result.Order.ItemsTotal);
        Assert.Equal(order.ShippingPrice, result.Order.ShippingPrice);
        Assert.Equal(order.TotalAmount, result.Order.TotalAmount);
        Assert.Equal(order.Currency, result.Order.Currency);
        Assert.Equal(order.CreatedAt, result.Order.CreatedAt);
    }

    [Fact]
    public async Task ComposeAsync_MapsTrackingEventsCorrectly()
    {
        var shipmentId = Guid.NewGuid();
        var order = BuildOrder(shipmentId);
        var shipment = BuildShipment(shipmentId);
        var trackingDto = BuildTracking();

        _orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _shipments.GetAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(shipment);
        _tracking.GetByShipmentAsync(shipmentId, Arg.Any<CancellationToken>()).Returns(trackingDto);

        var result = await _sut.ComposeAsync(order.Id, CancellationToken.None);

        Assert.NotNull(result?.Tracking);
        Assert.Equal(trackingDto.Events.Count, result.Tracking.Events.Count);

        var firstEvent = result.Tracking.Events[0];
        var firstDto = trackingDto.Events[0];
        Assert.Equal(firstDto.Status, firstEvent.Status);
        Assert.Equal(firstDto.Description, firstEvent.Description);
        Assert.Equal(firstDto.Location?.City, firstEvent.City);
        Assert.Equal(firstDto.OccurredAt, firstEvent.OccurredAt);
    }

    private static OrderDto BuildOrder(Guid? shipmentId) => new(
        Id: Guid.NewGuid(),
        Status: "CONFIRMED",
        ItemsTotal: 299.90m,
        ShippingPrice: 19.90m,
        TotalAmount: 319.80m,
        Currency: "BRL",
        CreatedAt: new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero),
        ShipmentId: shipmentId);

    private static ShipmentDto BuildShipment(Guid shipmentId) => new(
        Id: shipmentId,
        Status: "IN_TRANSIT",
        CarrierCode: "JADLOG",
        TrackingCode: "JD123456789BR",
        PromisedDeliveryDate: new DateOnly(2026, 7, 5));

    private static TrackingDto BuildTracking() => new(
        CurrentStatus: "IN_TRANSIT",
        LastLocation: new LocationDto("São Paulo", "SP"),
        LastEventOccurredAt: new DateTimeOffset(2026, 6, 29, 8, 0, 0, TimeSpan.Zero),
        EstimatedDeliveryDate: new DateOnly(2026, 7, 5),
        Events:
        [
            new TrackingEventDto(
                Status: "COLLECTED",
                Description: "Objeto coletado pelo transportador",
                Location: new LocationDto("Campinas", "SP"),
                OccurredAt: new DateTimeOffset(2026, 6, 28, 14, 0, 0, TimeSpan.Zero)),
            new TrackingEventDto(
                Status: "IN_TRANSIT",
                Description: "Em trânsito para o centro de distribuição",
                Location: new LocationDto("São Paulo", "SP"),
                OccurredAt: new DateTimeOffset(2026, 6, 29, 8, 0, 0, TimeSpan.Zero))
        ]);
}
