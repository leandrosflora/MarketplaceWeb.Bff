using Microsoft.AspNetCore.Http;

namespace MarketplaceWeb.Bff.Clients;

public static class DownstreamResponse
{
    public static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string serviceName)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new DownstreamApiException(serviceName, (int)response.StatusCode, body);
    }
}

public sealed class DownstreamApiException : Exception
{
    public DownstreamApiException(string serviceName, int statusCode, string responseBody, Exception? innerException = null)
        : base($"{serviceName} returned HTTP {statusCode}", innerException)
    {
        ServiceName = serviceName;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public static DownstreamApiException Timeout(string serviceName, Exception innerException) =>
        new(serviceName, StatusCodes.Status504GatewayTimeout, "The downstream request timed out.", innerException);

    public static DownstreamApiException Unavailable(string serviceName, Exception innerException) =>
        new(serviceName, StatusCodes.Status503ServiceUnavailable, "The downstream service is unavailable.", innerException);

    public string ServiceName { get; }
    public int StatusCode { get; }
    public string ResponseBody { get; }
}
