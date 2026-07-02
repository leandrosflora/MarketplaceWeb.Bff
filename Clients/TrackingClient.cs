using System.Net;
using System.Net.Http.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface ITrackingClient
{
    Task<TrackingDto?> GetByShipmentAsync(Guid shipmentId, CancellationToken cancellationToken);
}

public sealed class TrackingClient(HttpClient httpClient) : ITrackingClient
{
    public async Task<TrackingDto?> GetByShipmentAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/v1/tracking/shipments/{shipmentId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await DownstreamResponse.EnsureSuccessAsync(response, "Tracking");

        return await response.Content.ReadFromJsonAsync<TrackingDto>(cancellationToken);
    }
}

public sealed record TrackingDto(
    string CurrentStatus,
    LocationDto? LastLocation,
    DateTimeOffset LastEventOccurredAt,
    DateOnly? EstimatedDeliveryDate,
    IReadOnlyList<TrackingEventDto> Events);

public sealed record TrackingEventDto(
    string Status,
    string? Description,
    LocationDto? Location,
    DateTimeOffset OccurredAt);

public sealed record LocationDto(string? City, string? State);
