namespace MovieApp.Infrastructure.Configuration;

public sealed class SocialAuthOptions
{
    public const string SectionName = "Authentication:Social";

    public GoogleSocialAuthOptions Google { get; set; } = new();

    public AppleSocialAuthOptions Apple { get; set; } = new();
}

public sealed class GoogleSocialAuthOptions
{
    public string[] ClientIds { get; set; } = [];
}

public sealed class AppleSocialAuthOptions
{
    public string[] ClientIds { get; set; } = [];
}
