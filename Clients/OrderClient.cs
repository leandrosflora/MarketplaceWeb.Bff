using System.Net;
using System.Net.Http.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface IOrderClient
{
    Task<OrderListDto> ListAsync(CancellationToken cancellationToken);
    Task<OrderDto?> GetAsync(Guid orderId, CancellationToken cancellationToken);
    Task<OrderDto> CancelAsync(Guid orderId, string idempotencyKey, CancellationToken cancellationToken);
}

public sealed class OrderClient(HttpClient httpClient) : IOrderClient
{
    public async Task<OrderListDto> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/orders", cancellationToken);
        await DownstreamResponse.EnsureSuccessAsync(response, "Order");

        return await response.Content.ReadFromJsonAsync<OrderListDto>(cancellationToken)
            ?? new OrderListDto([]);
    }

    public async Task<OrderDto?> GetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/orders/{orderId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await DownstreamResponse.EnsureSuccessAsync(response, "Order");

        return await response.Content.ReadFromJsonAsync<OrderDto>(cancellationToken);
    }

    public async Task<OrderDto> CancelAsync(Guid orderId, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/orders/{orderId}/cancel");
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await DownstreamResponse.EnsureSuccessAsync(response, "Order");

        return await response.Content.ReadFromJsonAsync<OrderDto>(cancellationToken)
            ?? throw new InvalidOperationException("Order returned an empty response");
    }
}

public sealed record OrderListDto(IReadOnlyList<OrderDto> Orders);

public sealed record OrderDto(
    Guid Id,
    string Status,
    decimal ItemsTotal,
    decimal ShippingPrice,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt,
    Guid? ShipmentId);
