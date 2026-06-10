using MarketplaceWeb.Bff.Clients;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MarketplaceWeb.Bff.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "BFF request failed. TraceId: {TraceId}", httpContext.TraceIdentifier);

        var problem = exception switch
        {
            BadHttpRequestException badRequest => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request",
                Detail = badRequest.Message
            },
            DownstreamApiException downstream when downstream.StatusCode == StatusCodes.Status404NotFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found"
            },
            DownstreamApiException downstream when downstream.StatusCode is StatusCodes.Status409Conflict or StatusCodes.Status422UnprocessableEntity => new ProblemDetails
            {
                Status = downstream.StatusCode,
                Title = "Business operation rejected",
                Detail = "The operation could not be completed."
            },
            DownstreamApiException downstream when downstream.StatusCode == StatusCodes.Status504GatewayTimeout => new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "Service timed out",
                Detail = "The downstream service did not respond in time."
            },
            DownstreamApiException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service temporarily unavailable"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected error"
            }
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problem.Status!.Value;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
