using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MovieApp.Api.Errors;

internal sealed class ApiProblemDetailsResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context) =>
        EnrichProblemDetails(context.Result, context.HttpContext);

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static void EnrichProblemDetails(IActionResult? result, HttpContext httpContext)
    {
        if (result is ObjectResult { Value: ProblemDetails problemDetails })
        {
            ApiProblemDetailsEnricher.Enrich(httpContext, problemDetails);
        }
        else if (result is BadRequestObjectResult { Value: ValidationProblemDetails validationProblemDetails })
        {
            ApiProblemDetailsEnricher.Enrich(httpContext, validationProblemDetails, ApiErrorCodes.ValidationFailed);
        }
    }
}
