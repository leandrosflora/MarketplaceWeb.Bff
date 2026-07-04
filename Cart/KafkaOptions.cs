namespace MarketplaceWeb.Bff.Cart;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string CartAbandonedTopic { get; set; } = "cart.abandoned";
}
