namespace MovieApp.Api.Errors;

internal static class ApiErrorCodes
{
    internal const string BadRequest = "BAD_REQUEST";
    internal const string ValidationFailed = "VALIDATION_FAILED";
    internal const string AuthenticationFailed = "AUTHENTICATION_FAILED";
    internal const string Forbidden = "FORBIDDEN";
    internal const string NotFound = "NOT_FOUND";
    internal const string Conflict = "CONFLICT";
    internal const string TooManyRequests = "TOO_MANY_REQUESTS";
    internal const string InternalError = "INTERNAL_ERROR";
}
