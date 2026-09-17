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
    internal const string SearchProviderUnavailable = "SEARCH_PROVIDER_UNAVAILABLE";
    internal const string TvShowFollowBaselineUnavailable = "TV_SHOW_FOLLOW_BASELINE_UNAVAILABLE";
    internal const string AiRecommendationUnavailable = "AI_RECOMMENDATION_UNAVAILABLE";
    internal const string InternalError = "INTERNAL_ERROR";
}
