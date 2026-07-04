using System.Text.Json.Serialization;

namespace MarketplaceWeb.Bff.Cart;

// traceId/spanId are intentionally omitted here (unlike domain-service envelopes) since this
// event is published without an outbox/OpenTelemetry producer pipeline — see design.md's
// "Abandoned cart detection: BFF-owned periodic scan, no outbox" decision.
public sealed record CartKafkaEventEnvelope<TPayload>(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("producer")] string Producer,
    [property: JsonPropertyName("payload")] TPayload Payload);

public sealed record CartAbandonedPayload(
    [property: JsonPropertyName("cartOwnerId")] string CartOwnerId,
    [property: JsonPropertyName("buyerId")] Guid? BuyerId,
    [property: JsonPropertyName("items")] IReadOnlyList<CartAbandonedItemPayload> Items,
    [property: JsonPropertyName("lastActivityAt")] DateTimeOffset LastActivityAt);

public sealed record CartAbandonedItemPayload(
    [property: JsonPropertyName("skuId")] Guid SkuId,
    [property: JsonPropertyName("quantity")] int Quantity);
