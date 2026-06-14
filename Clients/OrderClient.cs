using System.Net;
using System.Net.Http.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface IOrderClient
{
    Task<OrderDto?> GetAsync(Guid orderId, CancellationToken cancellationToken);
    Task CancelAsync(Guid orderId, CancelOrderRequest request, string idempotencyKey, CancellationToken cancellationToken);
}

public sealed class OrderClient(HttpClient httpClient) : IOrderClient
{
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

    public async Task CancelAsync(Guid orderId, CancelOrderRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/orders/{orderId}/cancel")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await DownstreamResponse.EnsureSuccessAsync(response, "Order");
    }
}

public sealed record OrderDto(
    Guid Id,
    string Status,
    decimal ItemsTotal,
    decimal ShippingPrice,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt,
    Guid? ShipmentId);

public sealed record CancelOrderRequest(string Reason);
