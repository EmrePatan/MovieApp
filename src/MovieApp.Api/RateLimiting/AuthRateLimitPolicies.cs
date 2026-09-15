namespace MovieApp.Api.RateLimiting;

public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";

    public const string Social = "auth-social";

    public const string Register = "auth-register";

    public const string ForgotPassword = "auth-forgot-password";

    public const string ResetPassword = "auth-reset-password";
}
