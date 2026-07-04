using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace MarketplaceWeb.Bff.Cart;

/// <summary>
/// Publishes `cart.abandoned` directly to Kafka with no outbox — an explicit, accepted deviation
/// from the outbox pattern used by domain services, since a missed/duplicate reminder is
/// low-severity (see design.md, "Abandoned cart detection" decision).
/// </summary>
public sealed class KafkaCartEventPublisher : ICartEventPublisher, IDisposable
{
    private const string SchemaVersion = "1.0";
    private const string Producer = "marketplaceweb-bff";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaCartEventPublisher> _logger;

    public KafkaCartEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaCartEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 250
        }).Build();
    }

    public async Task PublishAbandonedAsync(string cartOwnerId, StoredCart cart, CancellationToken cancellationToken)
    {
        CartOwnerIdHelper.TryGetBuyerId(cartOwnerId, out var buyerId);

        var envelope = new CartKafkaEventEnvelope<CartAbandonedPayload>(
            EventId: Guid.NewGuid(),
            EventType: _options.CartAbandonedTopic,
            SchemaVersion: SchemaVersion,
            OccurredAt: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N"),
            Producer: Producer,
            Payload: new CartAbandonedPayload(
                cartOwnerId,
                buyerId == Guid.Empty ? null : buyerId,
                cart.Lines.Select(line => new CartAbandonedItemPayload(line.SkuId, line.Quantity)).ToList(),
                cart.LastModifiedAt));

        var payload = JsonSerializer.Serialize(envelope, JsonOptions);

        var result = await _producer.ProduceAsync(_options.CartAbandonedTopic, new Message<string, string>
        {
            Key = cartOwnerId,
            Value = payload
        }, cancellationToken);

        _logger.LogInformation(
            "Published cart.abandoned for cart owner {CartOwnerId} (eventId {EventId}) at offset {Offset}",
            cartOwnerId,
            envelope.EventId,
            result.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}
