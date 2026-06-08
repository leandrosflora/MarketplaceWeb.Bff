namespace MarketplaceWeb.Bff.Infrastructure;

public sealed class CorrelationIdHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private const string HeaderName = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext;
        var correlationId =
            context?.Request.Headers[HeaderName].FirstOrDefault()
            ?? context?.TraceIdentifier
            ?? Guid.NewGuid().ToString("N");

        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
