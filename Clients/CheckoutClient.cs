using System.Net;
using System.Net.Http.Json;
using MarketplaceWeb.Bff.Contracts;

namespace MarketplaceWeb.Bff.Clients;

public interface ICheckoutClient
{
    Task<CheckoutPageResponse> CreateAsync(CreateCheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CheckoutPageResponse?> GetAsync(Guid checkoutId, CancellationToken cancellationToken);
    Task<CheckoutPageResponse> ConfirmAsync(Guid checkoutId, ConfirmCheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken);
}

public sealed class CheckoutClient(HttpClient httpClient) : ICheckoutClient
{
    private const string DefaultCurrency = "BRL";

    public async Task<CheckoutPageResponse> CreateAsync(CreateCheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        var response = await PostAsync<CreateCheckoutRequest, CheckoutResponse>("/v1/checkouts", request, idempotencyKey, cancellationToken);

        return ToPageResponse(response, ToResponseItems(request.Items));
    }

    public async Task<CheckoutPageResponse?> GetAsync(Guid checkoutId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/v1/checkouts/{checkoutId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await DownstreamResponse.EnsureSuccessAsync(response, "Checkout");

        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponse>(cancellationToken);

        return checkout is null ? null : ToPageResponse(checkout, []);
    }

    public async Task<CheckoutPageResponse> ConfirmAsync(Guid checkoutId, ConfirmCheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        var response = await PostAsync<ConfirmCheckoutRequest, CheckoutResponse>($"/v1/checkouts/{checkoutId}/confirm", request, idempotencyKey, cancellationToken);

        return ToPageResponse(response, []);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };

        request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        await DownstreamResponse.EnsureSuccessAsync(response, "Checkout");

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Checkout returned an empty response");
    }

    private static IReadOnlyList<CheckoutItemResponse> ToResponseItems(IReadOnlyList<CheckoutItemRequest> items) =>
        items.Select(item => new CheckoutItemResponse(item.SkuId, item.Quantity)).ToList();

    private static CheckoutPageResponse ToPageResponse(CheckoutResponse response, IReadOnlyList<CheckoutItemResponse> items) =>
        new(
            response.CheckoutId,
            response.ItemsTotal,
            response.ShippingCost,
            response.TotalAmount,
            DefaultCurrency,
            ToResponse(response.ShippingOption),
            items);

    private static ShippingOptionResponse ToResponse(ShippingOptionDto shippingOption) =>
        new(
            shippingOption.PromiseId,
            shippingOption.Mode,
            shippingOption.Carrier,
            shippingOption.EstimatedDeliveryDate,
            shippingOption.Cost);
}

public sealed record CheckoutResponse(
    Guid CheckoutId,
    string Status,
    decimal ItemsTotal,
    decimal ShippingCost,
    decimal TotalAmount,
    ShippingOptionDto ShippingOption,
    DateTimeOffset ExpiresAt);

public sealed record ShippingOptionDto(
    string? PromiseId,
    string? Mode,
    string? Carrier,
    DateOnly? EstimatedDeliveryDate,
    decimal Cost);
