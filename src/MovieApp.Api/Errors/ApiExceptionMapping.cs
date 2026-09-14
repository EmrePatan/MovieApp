namespace MovieApp.Api.Errors;

internal sealed record ApiExceptionMapping(
    int Status,
    string Title,
    string Detail,
    string Code,
    bool LogAsError);

internal static class ApiExceptionMappings
{
    internal static ApiExceptionMapping InternalServerError { get; } = new(
        StatusCodes.Status500InternalServerError,
        "Internal Server Error",
        "An unexpected error occurred.",
        ApiErrorCodes.InternalError,
        LogAsError: true);

    internal static bool TryMap(Exception exception, out ApiExceptionMapping mapping)
    {
        mapping = null!;

        switch (exception)
        {
            case MovieApp.Application.Exceptions.ValidationException validation:
                mapping = new ApiExceptionMapping(
                    StatusCodes.Status400BadRequest,
                    "Validation failed.",
                    validation.Message,
                    ApiErrorCodes.ValidationFailed,
                    LogAsError: false);
                return true;
            case MovieApp.Application.Exceptions.AuthenticationException authentication:
                mapping = new ApiExceptionMapping(
                    StatusCodes.Status401Unauthorized,
                    "Authentication failed.",
                    authentication.Message,
                    ApiErrorCodes.AuthenticationFailed,
                    LogAsError: false);
                return true;
            case MovieApp.Application.Exceptions.NotFoundException notFound:
                mapping = new ApiExceptionMapping(
                    StatusCodes.Status404NotFound,
                    "Resource not found.",
                    notFound.Message,
                    ApiErrorCodes.NotFound,
                    LogAsError: false);
                return true;
            case MovieApp.Application.Exceptions.ConflictException conflict:
                mapping = new ApiExceptionMapping(
                    StatusCodes.Status409Conflict,
                    "Conflict.",
                    conflict.Message,
                    ApiErrorCodes.Conflict,
                    LogAsError: false);
                return true;
            case MovieApp.Application.Exceptions.SearchProviderUnavailableException:
                mapping = new ApiExceptionMapping(
                    StatusCodes.Status503ServiceUnavailable,
                    "Search provider unavailable.",
                    "Search provider is temporarily unavailable.",
                    ApiErrorCodes.SearchProviderUnavailable,
                    LogAsError: false);
                return true;
            case MovieApp.Application.Exceptions.TvShowFollowBaselineException baseline:
                mapping = new ApiExceptionMapping(
                    StatusCodes.Status503ServiceUnavailable,
                    "Follow baseline unavailable.",
                    baseline.Message,
                    ApiErrorCodes.TvShowFollowBaselineUnavailable,
                    LogAsError: false);
                return true;
            default:
                return false;
        }
    }

    internal static ApiExceptionMapping FromStatusCode(int? statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => new(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "The request could not be processed.",
                ApiErrorCodes.BadRequest,
                LogAsError: false),
            StatusCodes.Status401Unauthorized => new(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication is required.",
                ApiErrorCodes.AuthenticationFailed,
                LogAsError: false),
            StatusCodes.Status403Forbidden => new(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "You do not have permission to perform this action.",
                ApiErrorCodes.Forbidden,
                LogAsError: false),
            StatusCodes.Status404NotFound => new(
                StatusCodes.Status404NotFound,
                "Not Found",
                "The requested resource was not found.",
                ApiErrorCodes.NotFound,
                LogAsError: false),
            StatusCodes.Status409Conflict => new(
                StatusCodes.Status409Conflict,
                "Conflict",
                "The request could not be completed due to a conflict.",
                ApiErrorCodes.Conflict,
                LogAsError: false),
            StatusCodes.Status429TooManyRequests => new(
                StatusCodes.Status429TooManyRequests,
                "Too Many Requests",
                "Too many attempts. Please try again later.",
                ApiErrorCodes.TooManyRequests,
                LogAsError: false),
            StatusCodes.Status503ServiceUnavailable => new(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                "Search provider is temporarily unavailable.",
                ApiErrorCodes.SearchProviderUnavailable,
                LogAsError: false),
            _ => InternalServerError
        };
}
