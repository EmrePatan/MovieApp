using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpEmailOptions> smtpOptions,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        var options = smtpOptions.Value;
        if (!options.IsConfigured())
        {
            throw new InvalidOperationException(
                "SMTP email sender is not configured. Set Authentication:Email:Smtp before sending password reset emails.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = "Reset your MovieApp password",
            Body = $"Use the link below to reset your password. If you did not request this, you can ignore this email.\n\n{resetUrl}",
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var client = CreateSmtpClient(options);

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            client.Credentials = new NetworkCredential(options.Username, options.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            EmailLogMessages.PasswordResetSendFailed(logger, exception, toEmail);

            throw;
        }
    }

    private static SmtpClient CreateSmtpClient(SmtpEmailOptions options) =>
        new(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl
        };
}
