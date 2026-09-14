using Microsoft.AspNetCore.Diagnostics;

namespace MovieApp.Api.Errors;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (RequestAbortExceptionHandling.IsRequestAbortedCancellation(httpContext, exception))
        {
            return true;
        }

        var traceId = httpContext.TraceIdentifier;
        ApiExceptionMapping mapping;

        if (ApiExceptionMappings.TryMap(exception, out var knownMapping))
        {
            mapping = knownMapping;

            if (mapping.LogAsError)
            {
                GlobalExceptionHandlerLogMessages.LogMappedException(
                    logger,
                    exception,
                    mapping.Status,
                    traceId);
            }
        }
        else
        {
            mapping = ApiExceptionMappings.InternalServerError;
            GlobalExceptionHandlerLogMessages.LogUnhandledException(logger, exception, traceId);
        }

        var problemDetails = ApiProblemDetailsEnricher.Create(httpContext, mapping);

        httpContext.Response.StatusCode = mapping.Status;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
