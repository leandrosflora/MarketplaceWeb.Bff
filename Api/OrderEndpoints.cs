using MarketplaceWeb.Bff.Application;
using MarketplaceWeb.Bff.Clients;
using MarketplaceWeb.Bff.Infrastructure;

namespace MarketplaceWeb.Bff.Api;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/web/v1/orders")
            .RequireAuthorization()
            .RequireRateLimiting("PerUser");

        group.MapGet("/", async (
            IOrderClient orderClient,
            CancellationToken cancellationToken) =>
        {
            var response = await orderClient.ListAsync(cancellationToken);
            return Results.Ok(response);
        });

        group.MapGet("/{orderId:guid}", async (
            Guid orderId,
            OrderPageComposer composer,
            CancellationToken cancellationToken) =>
        {
            var response = await composer.ComposeAsync(orderId, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        group.MapPost("/{orderId:guid}/cancel", async (
                Guid orderId,
                HttpContext context,
                IOrderClient orderClient,
                CancellationToken cancellationToken) =>
            {
                var response = await orderClient.CancelAsync(
                    orderId,
                    GetRequiredIdempotencyKey(context),
                    cancellationToken);

                return Results.Ok(response);
            })
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        group.MapGet("/{orderId:guid}/tracking", async (
            Guid orderId,
            OrderPageComposer composer,
            CancellationToken cancellationToken) =>
        {
            var response = await composer.ComposeAsync(orderId, cancellationToken);
            return response is null
                ? Results.NotFound()
                : response.Tracking is null
                    ? Results.NotFound()
                    : Results.Ok(response.Tracking);
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
