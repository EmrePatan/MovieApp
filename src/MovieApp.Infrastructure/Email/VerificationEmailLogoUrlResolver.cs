namespace MovieApp.Infrastructure.Email;

public static class VerificationEmailLogoUrlResolver
{
    public const string DefaultLogoImagePath = "/email-assets/movie-cave-horizontal-logo-v1.png";

    public static string? Resolve(string? publicBaseUrl) =>
        VerificationEmailHeroUrlResolver.Resolve(DefaultLogoImagePath, publicBaseUrl);
}
