namespace MovieApp.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string Issuer { get; set; } = "MovieApp";

    public string Audience { get; set; } = "MovieApp.Mobile";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    public bool IsConfigured() => !string.IsNullOrWhiteSpace(SigningKey);
}
