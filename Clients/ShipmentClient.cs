using System.Net;
using System.Net.Http.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface IShipmentClient
{
    Task<ShipmentDto?> GetAsync(Guid shipmentId, CancellationToken cancellationToken);
    Task<Stream> GetLabelAsync(Guid shipmentId, CancellationToken cancellationToken);
}

public sealed class ShipmentClient(HttpClient httpClient) : IShipmentClient
{
    public async Task<ShipmentDto?> GetAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/shipments/{shipmentId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await DownstreamResponse.EnsureSuccessAsync(response, "Shipment");

        return await response.Content.ReadFromJsonAsync<ShipmentDto>(cancellationToken);
    }

    public async Task<Stream> GetLabelAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/shipments/{shipmentId}/label", cancellationToken);
        await DownstreamResponse.EnsureSuccessAsync(response, "Shipment");

        var memory = new MemoryStream();
        await response.Content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
    }
}

public sealed record ShipmentDto(
    Guid Id,
    string Status,
    string CarrierCode,
    string? TrackingCode,
    DateOnly PromisedDeliveryDate);
