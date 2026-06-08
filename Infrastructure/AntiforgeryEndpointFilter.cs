using Microsoft.AspNetCore.Antiforgery;

namespace MarketplaceWeb.Bff.Infrastructure;

public sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        await antiforgery.ValidateRequestAsync(context.HttpContext);
        return await next(context);
    }
}
