using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface ISocialAuthService
{
    Task<AuthenticationResult> AuthenticateAsync(
        SocialAuthRequest request,
        CancellationToken cancellationToken = default);
}
