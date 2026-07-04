using MarketplaceWeb.Bff.Cart;
using MarketplaceWeb.Bff.Contracts;

namespace MarketplaceWeb.Bff.Api;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/web/v1/cart")
            .RequireRateLimiting("PerUser");

        group.MapGet("/", async (
            string cartOwnerId,
            CartService cartService,
            CancellationToken cancellationToken) =>
        {
            var response = await cartService.GetAsync(RequireOwnerId(cartOwnerId), cancellationToken);
            return Results.Ok(response);
        });

        group.MapPost("/items", async (
            string cartOwnerId,
            AddCartItemRequest request,
            CartService cartService,
            CancellationToken cancellationToken) =>
        {
            var response = await cartService.AddItemAsync(RequireOwnerId(cartOwnerId), request, cancellationToken);
            return Results.Ok(response);
        });

        group.MapPut("/items/{skuId:guid}", async (
            string cartOwnerId,
            Guid skuId,
            UpdateCartItemQuantityRequest request,
            CartService cartService,
            CancellationToken cancellationToken) =>
        {
            var response = await cartService.UpdateQuantityAsync(RequireOwnerId(cartOwnerId), skuId, request.Quantity, cancellationToken);
            return Results.Ok(response);
        });

        group.MapDelete("/items/{skuId:guid}", async (
            string cartOwnerId,
            Guid skuId,
            CartService cartService,
            CancellationToken cancellationToken) =>
        {
            var response = await cartService.RemoveItemAsync(RequireOwnerId(cartOwnerId), skuId, cancellationToken);
            return Results.Ok(response);
        });

        group.MapPost("/merge", async (
            string anonymousCartOwnerId,
            string buyerCartOwnerId,
            CartService cartService,
            CancellationToken cancellationToken) =>
        {
            await cartService.MergeAnonymousCartAsync(
                RequireOwnerId(anonymousCartOwnerId),
                RequireOwnerId(buyerCartOwnerId),
                cancellationToken);

            return Results.NoContent();
        });

        group.MapPost("/checkout", async (
            string cartOwnerId,
            ProceedToCheckoutRequest request,
            CartService cartService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await cartService.ProceedToCheckoutAsync(RequireOwnerId(cartOwnerId), request, cancellationToken);
                return Results.Ok(response);
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }
        });

        return app;
    }

    private static string RequireOwnerId(string cartOwnerId)
    {
        if (string.IsNullOrWhiteSpace(cartOwnerId))
        {
            throw new BadHttpRequestException("cartOwnerId is required");
        }

        return cartOwnerId;
    }
}
