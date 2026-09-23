namespace MovieApp.Infrastructure.Configuration;

public sealed class ReviewTranslationRateLimitOptions
{
    public const string SectionName = "ReviewTranslation:RateLimit";

    public int PermitLimit { get; set; } = 30;

    public int WindowMinutes { get; set; } = 15;
}
