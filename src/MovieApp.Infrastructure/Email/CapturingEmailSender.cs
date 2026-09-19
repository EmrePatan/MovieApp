using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Infrastructure.Email;

/// <summary>
/// Captures password reset and verification emails for integration tests. Registered only in the Testing environment.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender, IEmailVerificationEmailSender, IPasswordResetEmailSender
{
    private readonly object _sync = new();
    private readonly List<(string Email, string ResetUrl)> _sentPasswordResetEmails = [];
    private readonly List<(Guid TokenId, string Email, string ResetUrl, string ContentLocale)> _sentPasswordResetDeliveryEmails = [];
    private readonly List<(Guid TokenId, string Email, string VerifyUrl, string ContentLocale)> _sentVerificationEmails = [];

    public IReadOnlyList<(Guid TokenId, string Email, string ResetUrl, string ContentLocale)> SentPasswordResetDeliveryEmails
    {
        get
        {
            lock (_sync)
            {
                return _sentPasswordResetDeliveryEmails.ToList();
            }
        }
    }

    public IReadOnlyList<(string Email, string ResetUrl)> SentEmails
    {
        get
        {
            lock (_sync)
            {
                return _sentPasswordResetEmails.ToList();
            }
        }
    }

    public IReadOnlyList<(Guid TokenId, string Email, string VerifyUrl, string ContentLocale)> SentVerificationEmails
    {
        get
        {
            lock (_sync)
            {
                return _sentVerificationEmails.ToList();
            }
        }
    }

    public Task SendPasswordResetEmailAsync(
        string toEmail,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _sentPasswordResetEmails.Add((toEmail, resetUrl));
        }

        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(
        Guid tokenId,
        string toEmail,
        string resetUrl,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _sentPasswordResetDeliveryEmails.Add((tokenId, toEmail, resetUrl, contentLocale));
            _sentPasswordResetEmails.Add((toEmail, resetUrl));
        }

        return Task.CompletedTask;
    }

    public Task SendVerificationEmailAsync(
        Guid tokenId,
        string toEmail,
        string verifyUrl,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _sentVerificationEmails.Add((tokenId, toEmail, verifyUrl, contentLocale));
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _sentPasswordResetEmails.Clear();
            _sentVerificationEmails.Clear();
        }
    }

    public string? ExtractTokenFromLastEmail()
    {
        lock (_sync)
        {
            if (_sentPasswordResetEmails.Count == 0)
            {
                return null;
            }

            return ExtractTokenFromUrl(_sentPasswordResetEmails[^1].ResetUrl);
        }
    }

    public string? ExtractTokenFromLastVerificationEmail()
    {
        lock (_sync)
        {
            if (_sentVerificationEmails.Count == 0)
            {
                return null;
            }

            return ExtractTokenFromUrl(_sentVerificationEmails[^1].VerifyUrl);
        }
    }

    public static string? ExtractTokenFromResetUrl(string resetUrl) =>
        ExtractTokenFromUrl(resetUrl);

    public static string? ExtractTokenFromVerificationUrl(string verifyUrl) =>
        ExtractTokenFromUrl(verifyUrl);

    public static string? ExtractTokenFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var queryIndex = url.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return null;
        }

        var query = url[(queryIndex + 1)..];
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 &&
                string.Equals(pair[0], "token", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[1]);
            }
        }

        return null;
    }
}
