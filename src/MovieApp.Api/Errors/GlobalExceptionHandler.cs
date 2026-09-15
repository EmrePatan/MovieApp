using Microsoft.AspNetCore.Diagnostics;
using MovieApp.Api.Observability;

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
        var correlationId = CorrelationIdAccessor.Get(httpContext) ?? traceId;
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
                    traceId,
                    correlationId);
            }
        }
        else
        {
            mapping = ApiExceptionMappings.InternalServerError;
            GlobalExceptionHandlerLogMessages.LogUnhandledException(
                logger,
                exception,
                traceId,
                correlationId);
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
