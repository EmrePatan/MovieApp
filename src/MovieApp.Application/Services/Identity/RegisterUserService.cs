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
    IResendVerificationService resendVerificationService) : IRegisterUserService
{
    public const string VerificationRequiredMessage =
        "Account created. Please check your email to verify your account before signing in.";

    public async Task<RegistrationResult> RegisterAsync(
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
        await resendVerificationService.SendVerificationEmailAsync(user, cancellationToken);

        return new RegistrationResult(
            UserMapper.ToCurrentUserResult(user),
            RequiresEmailVerification: true,
            VerificationRequiredMessage);
    }
}
