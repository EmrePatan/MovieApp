namespace MovieApp.Application.Abstractions.Identity;

public interface IEmailVerificationEmailSender
{
    Task SendVerificationEmailAsync(
        Guid tokenId,
        string toEmail,
        string verifyUrl,
        CancellationToken cancellationToken = default);
}
