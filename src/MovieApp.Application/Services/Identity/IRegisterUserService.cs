using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IRegisterUserService
{
    Task<RegistrationResult> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default);
}
