using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Infrastructure.Email;

/// <summary>
/// Captures password reset emails for integration tests. Registered only in the Testing environment.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly object _sync = new();
    private readonly List<(string Email, string ResetUrl)> _sentEmails = [];

    public IReadOnlyList<(string Email, string ResetUrl)> SentEmails
    {
        get
        {
            lock (_sync)
            {
                return _sentEmails.ToList();
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
            _sentEmails.Add((toEmail, resetUrl));
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _sentEmails.Clear();
        }
    }

    public string? ExtractTokenFromLastEmail()
    {
        lock (_sync)
        {
            if (_sentEmails.Count == 0)
            {
                return null;
            }

            return ExtractTokenFromResetUrl(_sentEmails[^1].ResetUrl);
        }
    }

    public static string? ExtractTokenFromResetUrl(string resetUrl)
    {
        if (string.IsNullOrWhiteSpace(resetUrl))
        {
            return null;
        }

        var queryIndex = resetUrl.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return null;
        }

        var query = resetUrl[(queryIndex + 1)..];
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
