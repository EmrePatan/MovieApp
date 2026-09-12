using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Api.Errors;

internal static class ApiProblemDetailsEnricher
{
    internal static void Enrich(HttpContext httpContext, ProblemDetails problemDetails, string? code = null)
    {
        var statusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        var resolvedCode = code ?? InferCode(statusCode);

        problemDetails.Status ??= statusCode;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        problemDetails.Extensions["code"] = resolvedCode;
        problemDetails.Type = ResolveErrorType(httpContext, resolvedCode);

        if (string.IsNullOrWhiteSpace(problemDetails.Instance))
        {
            problemDetails.Instance = httpContext.Request.Path;
        }
    }

    internal static ProblemDetails Create(HttpContext httpContext, ApiExceptionMapping mapping)
    {
        var problemDetails = new ProblemDetails
        {
            Status = mapping.Status,
            Title = mapping.Title,
            Detail = mapping.Detail
        };

        Enrich(httpContext, problemDetails, mapping.Code);
        return problemDetails;
    }

    private static string InferCode(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => ApiErrorCodes.ValidationFailed,
            StatusCodes.Status401Unauthorized => ApiErrorCodes.AuthenticationFailed,
            StatusCodes.Status403Forbidden => ApiErrorCodes.Forbidden,
            StatusCodes.Status404NotFound => ApiErrorCodes.NotFound,
            StatusCodes.Status409Conflict => ApiErrorCodes.Conflict,
            StatusCodes.Status429TooManyRequests => ApiErrorCodes.TooManyRequests,
            StatusCodes.Status500InternalServerError => ApiErrorCodes.InternalError,
            _ => ApiErrorCodes.BadRequest
        };

    private static string ResolveErrorType(HttpContext httpContext, string errorCode)
    {
        var publicBaseUrl = httpContext.RequestServices
            .GetService<IOptions<AppOptions>>()?.Value.PublicBaseUrl;

        var slug = errorCode.Replace('_', '-').ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(publicBaseUrl) &&
            Uri.TryCreate(publicBaseUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            return $"{baseUri.AbsoluteUri.TrimEnd('/')}/errors/{slug}";
        }

        return $"urn:movieapp:error:{errorCode.ToLowerInvariant()}";
    }
}
