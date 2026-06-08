using MarketplaceWeb.Bff.Clients;

namespace MarketplaceWeb.Bff.Api;

public static class ShippingEndpoints
{
    public static IEndpointRouteBuilder MapShippingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/web/v1/shipping-promises")
            .RequireRateLimiting("PerUser");

        group.MapPost("/", async (
            ShippingPromiseRequest request,
            IShippingPromiseClient shippingPromise,
            CancellationToken cancellationToken) =>
        {
            var response = await shippingPromise.CalculateAsync(request, cancellationToken);
            return Results.Ok(response);
        });

        return app;
    }
}
