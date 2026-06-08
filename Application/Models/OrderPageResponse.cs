namespace MarketplaceWeb.Bff.Application.Models;

public sealed record OrderPageResponse(
    OrderSummary Order,
    ShipmentSummary? Shipment,
    TrackingSummary? Tracking,
    IReadOnlyList<string> Warnings);

public sealed record OrderSummary(
    Guid OrderId,
    string Status,
    decimal ItemsTotal,
    decimal ShippingPrice,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed record ShipmentSummary(
    Guid ShipmentId,
    string Status,
    string CarrierCode,
    string? TrackingCode,
    DateOnly PromisedDeliveryDate);

public sealed record TrackingSummary(
    string CurrentStatus,
    string? LastCity,
    string? LastState,
    DateTimeOffset LastUpdate,
    DateOnly? EstimatedDeliveryDate,
    IReadOnlyList<TrackingEventSummary> Events);

public sealed record TrackingEventSummary(
    string Status,
    string? Description,
    string? City,
    string? State,
    DateTimeOffset OccurredAt);
