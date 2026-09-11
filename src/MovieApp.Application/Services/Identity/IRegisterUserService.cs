using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IRegisterUserService
{
    Task<AuthenticationResult> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default);
}
