using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IUserExternalLoginRepository
{
    Task<User?> GetUserByProviderAndSubjectAsync(
        string provider,
        string providerSubject,
        CancellationToken cancellationToken = default);

    Task<UserExternalLogin> CreateAsync(
        UserExternalLogin externalLogin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetProvidersForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default);

    Task UpdateProviderPictureUrlAsync(
        Guid userId,
        string provider,
        string? pictureUrl,
        CancellationToken cancellationToken = default);
}
