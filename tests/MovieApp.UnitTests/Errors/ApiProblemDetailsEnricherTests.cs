using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Api.Errors;
using MovieApp.Api.Observability;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Errors;

public sealed class ApiProblemDetailsEnricherTests
{
    [Fact]
    public void EnrichAddsTraceIdCodeAndConfiguredErrorType()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-test-123",
            Request = { Path = "/api/movies/search" }
        };

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IOptions<AppOptions>>(Options.Create(new AppOptions
            {
                PublicBaseUrl = "https://api.example.com"
            }))
            .BuildServiceProvider();

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred."
        };

        ApiProblemDetailsEnricher.Enrich(httpContext, problemDetails, ApiErrorCodes.InternalError);

        Assert.Equal("trace-test-123", problemDetails.Extensions["traceId"]);
        Assert.Equal(ApiErrorCodes.InternalError, problemDetails.Extensions["code"]);
        Assert.Equal("https://api.example.com/errors/internal-error", problemDetails.Type);
        Assert.Equal("/api/movies/search", problemDetails.Instance);
    }

    [Fact]
    public void EnrichAddsCorrelationIdFromAccessor()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-correlation"
        };
        CorrelationIdAccessor.Set(httpContext, "operator-correlation-001");

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IOptions<AppOptions>>(Options.Create(new AppOptions()))
            .BuildServiceProvider();

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError
        };

        ApiProblemDetailsEnricher.Enrich(httpContext, problemDetails, ApiErrorCodes.InternalError);

        Assert.Equal("operator-correlation-001", problemDetails.Extensions["correlationId"]);
    }

    [Fact]
    public void EnrichUsesStableUrnWhenPublicBaseUrlIsMissing()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-test-456"
        };

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IOptions<AppOptions>>(Options.Create(new AppOptions()))
            .BuildServiceProvider();

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = "Missing."
        };

        ApiProblemDetailsEnricher.Enrich(httpContext, problemDetails, ApiErrorCodes.NotFound);

        Assert.Equal("urn:movieapp:error:not_found", problemDetails.Type);
    }

    [Fact]
    public void CreateUsesGenericDetailForInternalServerError()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-internal"
        };

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IOptions<AppOptions>>(Options.Create(new AppOptions()))
            .BuildServiceProvider();

        var problemDetails = ApiProblemDetailsEnricher.Create(
            httpContext,
            ApiExceptionMappings.InternalServerError);

        Assert.Equal(StatusCodes.Status500InternalServerError, problemDetails.Status);
        Assert.Equal("An unexpected error occurred.", problemDetails.Detail);
        Assert.Equal(ApiErrorCodes.InternalError, problemDetails.Extensions["code"]);
    }
}
