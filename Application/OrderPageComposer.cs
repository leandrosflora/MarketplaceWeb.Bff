using MarketplaceWeb.Bff.Application.Models;
using MarketplaceWeb.Bff.Clients;

namespace MarketplaceWeb.Bff.Application;

public sealed class OrderPageComposer(
    IOrderClient orders,
    IShipmentClient shipments,
    ITrackingClient tracking)
{
    public async Task<OrderPageResponse?> ComposeAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(orderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var warnings = new List<string>();
        ShipmentDto? shipment = null;
        TrackingDto? trackingDto = null;

        if (order.ShipmentId.HasValue)
        {
            var shipmentTask = SafeCallAsync(() => shipments.GetAsync(order.ShipmentId.Value, cancellationToken));
            var trackingTask = SafeCallAsync(() => tracking.GetByShipmentAsync(order.ShipmentId.Value, cancellationToken));

            await Task.WhenAll(shipmentTask, trackingTask);

            shipment = await shipmentTask;
            trackingDto = await trackingTask;

            if (shipment is null)
            {
                warnings.Add("Os dados da entrega estão temporariamente indisponíveis.");
            }

            if (trackingDto is null)
            {
                warnings.Add("O rastreamento está temporariamente indisponível.");
            }
        }

        return new OrderPageResponse(
            new OrderSummary(
                order.Id,
                order.Status,
                order.ItemsTotal,
                order.ShippingPrice,
                order.TotalAmount,
                order.Currency,
                order.CreatedAt),
            shipment is null
                ? null
                : new ShipmentSummary(
                    shipment.Id,
                    shipment.Status,
                    shipment.CarrierCode,
                    shipment.TrackingCode,
                    shipment.PromisedDeliveryDate),
            trackingDto is null
                ? null
                : new TrackingSummary(
                    trackingDto.CurrentStatus,
                    trackingDto.LastLocation?.City,
                    trackingDto.LastLocation?.State,
                    trackingDto.LastEventOccurredAt,
                    trackingDto.EstimatedDeliveryDate,
                    trackingDto.Events.Select(x => new TrackingEventSummary(
                            x.Status,
                            x.Description,
                            x.Location?.City,
                            x.Location?.State,
                            x.OccurredAt))
                        .ToList()),
            warnings);
    }

    private static async Task<T?> SafeCallAsync<T>(Func<Task<T?>> action)
    {
        try
        {
            return await action();
        }
        catch
        {
            return default;
        }
    }
}
