using MarketplaceWeb.Bff.Application;
using MarketplaceWeb.Bff.Clients;

namespace MarketplaceWeb.Bff.Api;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/web/v1/products")
            .RequireRateLimiting("PerUser");

        group.MapGet("/search", async (
            string? query,
            string? q,
            int? page,
            int? pageSize,
            IProductSearchClient productSearch,
            CancellationToken cancellationToken) =>
        {
            var searchText = !string.IsNullOrWhiteSpace(query) ? query : q;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                throw new BadHttpRequestException("query is required");
            }

            var response = await productSearch.SearchAsync(
                searchText.Trim(),
                Math.Max(page ?? 1, 1),
                Math.Clamp(pageSize ?? 20, 1, 100),
                cancellationToken);
            return Results.Ok(response);
        });

        group.MapGet("/{skuId:guid}", async (
                Guid skuId,
                IProductCatalogClient productCatalog,
                CancellationToken cancellationToken) =>
            {
                var product = await productCatalog.GetAsync(skuId, cancellationToken);
                return product is null ? Results.NotFound() : Results.Ok(product);
            })
            .CacheOutput("PublicProduct");

        group.MapGet("/{skuId:guid}/page", async (
            Guid skuId,
            int? quantity,
            string? zipCode,
            ProductPageComposer composer,
            CancellationToken cancellationToken) =>
        {
            var response = await composer.ComposeAsync(
                skuId,
                Math.Clamp(quantity ?? 1, 1, 99),
                zipCode,
                cancellationToken);

            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        return app;
    }
}
