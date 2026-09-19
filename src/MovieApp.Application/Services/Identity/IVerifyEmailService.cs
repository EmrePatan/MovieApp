using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IVerifyEmailService
{
    Task<AuthenticationResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default);
}
