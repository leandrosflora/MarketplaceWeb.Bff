namespace MarketplaceWeb.Bff.Infrastructure;

public sealed class W3CTraceContextHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var activity = System.Diagnostics.Activity.Current;
        if (activity is not null)
        {
            request.Headers.Remove("traceparent");
            request.Headers.TryAddWithoutValidation("traceparent", activity.Id);

            if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
            {
                request.Headers.Remove("tracestate");
                request.Headers.TryAddWithoutValidation("tracestate", activity.TraceStateString);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
