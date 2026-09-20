using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IUserProfileService
{
    Task<UserProfileResult> GetCurrentProfileAsync(CancellationToken cancellationToken = default);

    Task<UserProfileResult> UpdateDisplayNameAsync(
        string displayName,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> ChangeEmailAsync(
        string email,
        string currentPassword,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<UserStatisticsResult> GetStatisticsAsync(
        string? timeZoneId = null,
        CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(DeleteAccountCommand request, CancellationToken cancellationToken = default);
}
