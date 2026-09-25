namespace MovieApp.Api.RateLimiting;

public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";

    public const string Social = "auth-social";

    public const string Register = "auth-register";

    public const string ForgotPassword = "auth-forgot-password";

    public const string ResetPassword = "auth-reset-password";

    public const string VerifyEmail = "auth-verify-email";

    public const string ResendVerification = "auth-resend-verification";

    public const string Refresh = "auth-refresh";
}
