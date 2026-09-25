namespace MovieApp.Application.Configuration;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "Authentication:RefreshToken";

    public int LifetimeDays { get; set; } = 30;
}
