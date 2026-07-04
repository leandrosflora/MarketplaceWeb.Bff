using MarketplaceWeb.Bff.Cart;
using MarketplaceWeb.Bff.Clients;
using MarketplaceWeb.Bff.Contracts;

namespace MarketplaceWeb.Bff.Api;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/web/v1/checkouts")
            .RequireRateLimiting("PerUser");

        group.MapPost("/{checkoutId:guid}/payment-method", async (
            Guid checkoutId,
            PaymentMethodRequest request,
            ICheckoutPaymentMethodStore paymentMethodStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.CardholderName)
                || string.IsNullOrWhiteSpace(request.MaskedCardNumber)
                || string.IsNullOrWhiteSpace(request.ExpiryMonthYear))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["paymentMethod"] = ["Cardholder name, card number, and expiry are required."]
                });
            }

            var paymentIntentId = $"pi_mock_{checkoutId:N}_{Guid.NewGuid():N}";
            await paymentMethodStore.SetPaymentIntentIdAsync(checkoutId, paymentIntentId, cancellationToken);

            return Results.Ok(new PaymentMethodResponse(checkoutId, paymentIntentId));
        });

        group.MapGet("/{checkoutId:guid}/payment-method", async (
            Guid checkoutId,
            ICheckoutPaymentMethodStore paymentMethodStore,
            CancellationToken cancellationToken) =>
        {
            var paymentIntentId = await paymentMethodStore.GetPaymentIntentIdAsync(checkoutId, cancellationToken);

            return paymentIntentId is null
                ? Results.NotFound()
                : Results.Ok(new PaymentMethodResponse(checkoutId, paymentIntentId));
        });

        group.MapPost("/", async (
            CreateCheckoutRequest request,
            HttpContext context,
            ICheckoutClient checkoutClient,
            CancellationToken cancellationToken) =>
        {
            var response = await checkoutClient.CreateAsync(
                request,
                GetRequiredIdempotencyKey(context),
                cancellationToken);

            return Results.Created($"/api/web/v1/checkouts/{response.CheckoutId}", response);
        });

        group.MapGet("/{checkoutId:guid}", async (
            Guid checkoutId,
            ICheckoutClient checkoutClient,
            CancellationToken cancellationToken) =>
        {
            var response = await checkoutClient.GetAsync(checkoutId, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        group.MapPost("/{checkoutId:guid}/confirm", async (
            Guid checkoutId,
            ConfirmCheckoutRequest request,
            HttpContext context,
            ICheckoutClient checkoutClient,
            CancellationToken cancellationToken) =>
        {
            var response = await checkoutClient.ConfirmAsync(
                checkoutId,
                request,
                GetRequiredIdempotencyKey(context),
                cancellationToken);

            return Results.Ok(response);
        });

        return app;
    }

    private static string GetRequiredIdempotencyKey(HttpContext context)
    {
        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BadHttpRequestException("Idempotency-Key is required");
        }

        return key;
    }
}
