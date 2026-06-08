using MarketplaceWeb.Bff.Clients;

namespace MarketplaceWeb.Bff.Api;

public static class ShipmentEndpoints
{
    public static IEndpointRouteBuilder MapShipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/web/v1/shipments")
            .RequireAuthorization()
            .RequireRateLimiting("PerUser");

        group.MapGet("/{shipmentId:guid}/label", async (
            Guid shipmentId,
            IShipmentClient shipmentClient,
            CancellationToken cancellationToken) =>
        {
            var label = await shipmentClient.GetLabelAsync(shipmentId, cancellationToken);
            return Results.File(label, "application/pdf", $"shipment-{shipmentId}-label.pdf");
        });

        return app;
    }
}
