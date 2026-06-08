using System.Net.Http.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface IShippingPromiseClient
{
    Task<ShippingPromiseDto> CalculateAsync(ShippingPromiseRequest request, CancellationToken cancellationToken);
}

public sealed class ShippingPromiseClient(HttpClient httpClient) : IShippingPromiseClient
{
    public async Task<ShippingPromiseDto> CalculateAsync(ShippingPromiseRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/shipping-promises", request, cancellationToken);

        await DownstreamResponse.EnsureSuccessAsync(response, "Shipping Promise");

        return await response.Content.ReadFromJsonAsync<ShippingPromiseDto>(cancellationToken)
            ?? throw new InvalidOperationException("Shipping Promise returned an empty response");
    }
}

public sealed record ShippingPromiseRequest(
    Guid BuyerId,
    Guid SellerId,
    AddressDto Destination,
    IReadOnlyList<ShippingPromiseItemDto> Items);

public sealed record ShippingPromiseItemDto(Guid SkuId, int Quantity, decimal UnitPrice);

public sealed record AddressDto(string ZipCode, string City, string State, string Country);

public sealed record ShippingPromiseDto(
    bool Available,
    string? PromiseId,
    string? Mode,
    string? Carrier,
    DateOnly? EstimatedDeliveryDate,
    decimal? Cost,
    string Source,
    string? UnavailableReason);
