namespace MovieApp.Api.RateLimiting;

public static class SearchRateLimitPolicies
{
    public const string UnifiedSearch = "search-unified";

    public const string MovieSearch = "search-movies";

    public const string TvSearch = "search-tvshows";
}

public static class AccountRateLimitPolicies
{
    public const string ChangePassword = "account-change-password";

    public const string ChangeEmail = "account-change-email";

    public const string DeleteAccount = "account-delete";
}
