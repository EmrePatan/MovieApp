using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class LoginUserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAuthenticationSessionService authenticationSessionService) : ILoginUserService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<AuthenticationResult> LoginAsync(
        LoginUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = LoginUserValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var normalizedEmail = UserEmailNormalizer.Normalize(request.Email);
        var user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || !user.HasPassword ||
            !passwordHasher.VerifyPassword(request.Password, user.PasswordHash!))
        {
            throw new AuthenticationException(InvalidCredentialsMessage);
        }

        if (!user.IsEmailVerified)
        {
            throw new EmailNotVerifiedException();
        }

        user.RecordSuccessfulLogin(DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        return await authenticationSessionService.IssueAsync(user, cancellationToken);
    }
}
