using MovieApp.Application.Abstractions.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Email;

public sealed class DevelopmentEmailSender(
    ILogger<DevelopmentEmailSender> logger,
    IHostEnvironment hostEnvironment) : IEmailSender
{
    public Task SendPasswordResetEmailAsync(
        string toEmail,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        if (hostEnvironment.IsProduction())
        {
            return Task.CompletedTask;
        }

        EmailLogMessages.PasswordResetQueued(logger, toEmail);

        return Task.CompletedTask;
    }
}
