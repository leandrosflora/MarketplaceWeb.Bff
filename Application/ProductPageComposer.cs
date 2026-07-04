using MarketplaceWeb.Bff.Application.Models;
using MarketplaceWeb.Bff.Clients;

namespace MarketplaceWeb.Bff.Application;

public sealed class ProductPageComposer(
    IProductCatalogClient productCatalog,
    IShippingPromiseClient shippingPromise)
{
    public async Task<ProductPageResponse?> ComposeAsync(
        Guid skuId,
        int quantity,
        string? zipCode,
        CancellationToken cancellationToken)
    {
        var product = await productCatalog.GetAsync(skuId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var warnings = new List<string>();
        ShippingSummary? shipping = null;

        if (!string.IsNullOrWhiteSpace(zipCode))
        {
            try
            {
                var promise = await shippingPromise.CalculateAsync(
                    new ShippingPromiseRequest(
                        product.SellerId,
                        product.SellerId,
                        new AddressDto(zipCode, string.Empty, string.Empty, "BR"),
                        [new ShippingPromiseItemDto(product.SkuId, quantity, product.Price)]),
                    cancellationToken);

                shipping = new ShippingSummary(
                    promise.Available,
                    promise.PromiseId,
                    promise.Mode,
                    promise.EstimatedDeliveryDate,
                    promise.Cost,
                    promise.UnavailableReason);
            }
            catch
            {
                warnings.Add("Não foi possível calcular o frete agora.");
            }
        }

        return new ProductPageResponse(
            new ProductSummary(
                product.SkuId,
                product.SellerId,
                product.Title,
                product.Category,
                product.Price,
                string.Equals(product.Status, "Active", StringComparison.OrdinalIgnoreCase),
                product.ImageUrl),
            shipping,
            warnings);
    }
}
