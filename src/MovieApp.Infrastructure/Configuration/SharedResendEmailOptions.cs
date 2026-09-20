namespace MovieApp.Infrastructure.Configuration;

public sealed class SharedResendEmailOptions
{
    public const string SectionName = "Authentication:Email:Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Movie Cave";

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}
