namespace MovieApp.Infrastructure.Configuration;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Authentication:Email:Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "MovieApp";

    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}
