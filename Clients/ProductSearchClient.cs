using System.Net.Http.Json;
using System.Text.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface IProductSearchClient
{
    Task<ProductSearchResponse> SearchAsync(string query, CancellationToken cancellationToken);
}

public sealed class ProductSearchClient(HttpClient httpClient) : IProductSearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProductSearchResponse> SearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/products/search?query={Uri.EscapeDataString(query)}";
            using var response = await httpClient.GetAsync(path, cancellationToken);

            await DownstreamResponse.EnsureSuccessAsync(response, "Product Search");

            var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            return content.ValueKind switch
            {
                JsonValueKind.Array => new ProductSearchResponse(
                    content.Deserialize<IReadOnlyList<ProductSearchItemDto>>(JsonOptions) ?? []),
                JsonValueKind.Object => content.Deserialize<ProductSearchResponse>(JsonOptions) ?? new ProductSearchResponse([]),
                _ => new ProductSearchResponse([])
            };
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw DownstreamApiException.Timeout("Product Search", exception);
        }
        catch (HttpRequestException exception)
        {
            throw DownstreamApiException.Unavailable("Product Search", exception);
        }
    }
}

public sealed record ProductSearchResponse(IReadOnlyList<ProductSearchItemDto> Products);

public sealed record ProductSearchItemDto(
    Guid SkuId,
    Guid SellerId,
    string Title,
    string Category,
    decimal Price,
    string Status,
    decimal? Score = null);
