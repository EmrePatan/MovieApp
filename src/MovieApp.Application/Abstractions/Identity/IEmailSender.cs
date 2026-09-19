namespace MovieApp.Application.Abstractions.Identity;

public interface IEmailSender
{
    Task SendPasswordResetEmailAsync(
        string toEmail,
        string resetUrl,
        CancellationToken cancellationToken = default);

    Task SendEmailVerificationEmailAsync(
        string toEmail,
        string verifyUrl,
        CancellationToken cancellationToken = default);
}
