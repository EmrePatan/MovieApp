using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class RegisterUserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IRegisterUserService
{
    public async Task<AuthenticationResult> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = RegisterUserValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var normalizedEmail = UserEmailNormalizer.Normalize(request.Email);
        if (await userRepository.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("A user with this email address already exists.");
        }

        var passwordHash = passwordHasher.HashPassword(request.Password);
        var user = User.Create(
            Guid.NewGuid(),
            request.Email,
            passwordHash,
            request.DisplayName,
            DateTime.UtcNow);

        await userRepository.CreateAsync(user, cancellationToken);

        var token = tokenService.CreateAccessToken(UserMapper.ToTokenUserContext(user));

        return new AuthenticationResult(
            token.AccessToken,
            token.ExpiresAt,
            UserMapper.ToCurrentUserResult(user));
    }
}
