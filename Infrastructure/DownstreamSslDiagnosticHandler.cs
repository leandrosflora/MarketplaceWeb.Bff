using System.Security.Authentication;

namespace MarketplaceWeb.Bff.Infrastructure;

public sealed class DownstreamSslDiagnosticHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception) when (IsCorruptedTlsFrame(exception))
        {
            var uri = request.RequestUri?.GetLeftPart(UriPartial.Authority) ?? "downstream service";

            throw new HttpRequestException(
                $"Unable to establish an HTTPS connection with {uri}. " +
                "The endpoint returned a non-TLS response during the TLS handshake. " +
                "Confirm that the configured Services URL uses https only for ports where the microservice is listening with HTTPS; " +
                "otherwise configure the service URL with http.",
                exception,
                exception.StatusCode);
        }
    }

    private static bool IsCorruptedTlsFrame(HttpRequestException exception) =>
        exception.InnerException is AuthenticationException authenticationException
        && authenticationException.Message.Contains(
            "Cannot determine the frame size or a corrupted frame was received",
            StringComparison.OrdinalIgnoreCase);
}
