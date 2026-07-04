using System.Text.Json;
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
                Detail = ExtractDownstreamDetail(downstream.ResponseBody) ?? "The operation could not be completed."
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
            HttpRequestException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service temporarily unavailable",
                Detail = "The downstream service could not be reached."
            },
            TaskCanceledException => new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "Service timed out",
                Detail = "The downstream service did not respond in time."
            },
            JsonException => new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Invalid downstream response",
                Detail = "The downstream service returned a response the BFF could not parse."
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

    private static string? ExtractDownstreamDetail(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("detail", out var detail)
                ? detail.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
