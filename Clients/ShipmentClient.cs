using System.Net;
using System.Net.Http.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface IShipmentClient
{
    Task<ShipmentDto?> GetAsync(Guid shipmentId, CancellationToken cancellationToken);
    Task<ShipmentLabelDto> GetLabelAsync(Guid shipmentId, CancellationToken cancellationToken);
}

public sealed class ShipmentClient(HttpClient httpClient) : IShipmentClient
{
    public async Task<ShipmentDto?> GetAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/v1/shipments/{shipmentId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await DownstreamResponse.EnsureSuccessAsync(response, "Shipment");

        return await response.Content.ReadFromJsonAsync<ShipmentDto>(cancellationToken);
    }

    public async Task<ShipmentLabelDto> GetLabelAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/v1/shipments/{shipmentId}/label", cancellationToken);
        await DownstreamResponse.EnsureSuccessAsync(response, "Shipment");

        return await response.Content.ReadFromJsonAsync<ShipmentLabelDto>(cancellationToken)
            ?? throw new InvalidOperationException("Shipment returned an empty label response");
    }
}

public sealed record ShipmentDto(
    Guid Id,
    string Status,
    string CarrierCode,
    string? TrackingCode,
    DateOnly PromisedDeliveryDate);

public sealed record ShipmentLabelDto(string Url, int ExpiresInSeconds);
