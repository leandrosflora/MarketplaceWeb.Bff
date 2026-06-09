using System.Net;
using System.Net.Http.Json;
using MarketplaceWeb.Bff.Contracts;

namespace MarketplaceWeb.Bff.Clients;

public interface ICheckoutClient
{
    Task<CheckoutResponse> CreateAsync(CreateCheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<CheckoutResponse?> GetAsync(Guid checkoutId, CancellationToken cancellationToken);
    Task<CheckoutResponse> ConfirmAsync(Guid checkoutId, ConfirmCheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken);
}

public sealed class CheckoutClient(HttpClient httpClient) : ICheckoutClient
{
    public Task<CheckoutResponse> CreateAsync(CreateCheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        PostAsync<CreateCheckoutRequest, CheckoutResponse>("/checkouts", request, idempotencyKey, cancellationToken);

    public async Task<CheckoutResponse?> GetAsync(Guid checkoutId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/checkouts/{checkoutId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await DownstreamResponse.EnsureSuccessAsync(response, "Checkout");

        return await response.Content.ReadFromJsonAsync<CheckoutResponse>(cancellationToken);
    }

    public Task<CheckoutResponse> ConfirmAsync(Guid checkoutId, ConfirmCheckoutRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        PostAsync<ConfirmCheckoutRequest, CheckoutResponse>($"/checkouts/{checkoutId}/confirm", request, idempotencyKey, cancellationToken);

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
}
