using System.Net;
using System.Net.Http.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface IProductCatalogClient
{
    Task<ProductDto?> GetAsync(Guid skuId, CancellationToken cancellationToken);
}

public sealed class ProductCatalogClient(HttpClient httpClient) : IProductCatalogClient
{
    public async Task<ProductDto?> GetAsync(Guid skuId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/products/{skuId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await DownstreamResponse.EnsureSuccessAsync(response, "Product Catalog");

        var res =  await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);

        return res;
    }
}

public sealed record ProductDto(
    Guid SkuId,
    Guid SellerId,
    string Title,
    string Category,
    decimal Price,
    string Status,
    decimal WeightKg,
    decimal HeightCm,
    decimal WidthCm,
    decimal LengthCm,
    bool IsFragile,
    bool IsRestricted);
