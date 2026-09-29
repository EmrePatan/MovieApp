using Microsoft.AspNetCore.Mvc;

namespace MovieApp.Api.Errors;

internal static class ApiProblemDetailsHelper
{
    internal static ProblemDetails Create(int statusCode, string title, string detail, string? code = null)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            problemDetails.Extensions["code"] = code;
        }

        return problemDetails;
    }
}
