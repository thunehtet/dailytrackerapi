using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace daily_tracker_api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled API exception. TraceId: {TraceId}; Path: {Path}",
            httpContext.TraceIdentifier,
            httpContext.Request.Path);

        var configurationError = exception is OptionsValidationException;
        var status = configurationError
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = configurationError
                    ? "The OpenAI service configuration is invalid."
                    : "An unexpected server error occurred.",
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            },
            cancellationToken);
        return true;
    }
}
