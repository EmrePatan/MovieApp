using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface ILoginUserService
{
    Task<AuthenticationResult> LoginAsync(
        LoginUserRequest request,
        CancellationToken cancellationToken = default);
}
