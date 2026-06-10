using System.Net.Http.Json;
using System.Text.Json;

namespace MarketplaceWeb.Bff.Clients;

public interface IProductSearchClient
{
    Task<ProductSearchResponse> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed class ProductSearchClient(HttpClient httpClient) : IProductSearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProductSearchResponse> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/v1/products/search?query={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}";
            using var response = await httpClient.GetAsync(path, cancellationToken);

            await DownstreamResponse.EnsureSuccessAsync(response, "Product Search");

            var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            return content.ValueKind switch
            {
                JsonValueKind.Array => new ProductSearchResponse(
                    content.Deserialize<IReadOnlyList<ProductSearchItemDto>>(JsonOptions) ?? [],
                    new ProductSearchPaginationDto(page, pageSize, 0)),
                JsonValueKind.Object => MapDownstreamResponse(content, page, pageSize),
                _ => new ProductSearchResponse([], new ProductSearchPaginationDto(page, pageSize, 0))
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

    private static ProductSearchResponse MapDownstreamResponse(JsonElement content, int page, int pageSize)
    {
        var downstreamResponse = content.Deserialize<ProductSearchDownstreamResponse>(JsonOptions);

        return new ProductSearchResponse(
            downstreamResponse?.Items ?? [],
            downstreamResponse?.Pagination ?? new ProductSearchPaginationDto(page, pageSize, 0));
    }
}

public sealed record ProductSearchResponse(
    IReadOnlyList<ProductSearchItemDto> Items,
    ProductSearchPaginationDto Pagination);

public sealed record ProductSearchItemDto(
    string ProductId,
    string Title,
    string ImageUrl,
    decimal Price,
    string SellerId,
    int AvailableQuantity,
    decimal Rating,
    bool FreeShipping,
    bool Fulfillment,
    string ShippingPromise);

public sealed record ProductSearchPaginationDto(
    int Page,
    int PageSize,
    int Total);

file sealed record ProductSearchDownstreamResponse(
    IReadOnlyList<ProductSearchItemDto> Items,
    ProductSearchPaginationDto Pagination);
