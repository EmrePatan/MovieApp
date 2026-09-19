namespace MovieApp.Application.Abstractions.Identity;

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetEmailAsync(
        Guid tokenId,
        string toEmail,
        string resetUrl,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
