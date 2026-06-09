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
    public DownstreamApiException(string serviceName, int statusCode, string responseBody)
        : base($"{serviceName} returned HTTP {statusCode}")
    {
        ServiceName = serviceName;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string ServiceName { get; }
    public int StatusCode { get; }
    public string ResponseBody { get; }
}
