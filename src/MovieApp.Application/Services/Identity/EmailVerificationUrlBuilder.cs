namespace MovieApp.Application.Services.Identity;

internal static class EmailVerificationUrlBuilder
{
    public static string BuildVerificationUrl(string baseUrl, string rawToken) =>
        EmailAuthActionUrlBuilder.Build(baseUrl, rawToken);
}
