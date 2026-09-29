using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Identity;

public interface IAccountReauthenticationService
{
    Task EnsureCurrentAccountReauthenticatedAsync(
        User user,
        IReadOnlyList<string> linkedProviders,
        AccountReauthenticationProof proof,
        CancellationToken cancellationToken = default);

    Task<VerifiedSocialIdentity> VerifySocialIdentityTokenAsync(
        string provider,
        string identityToken,
        CancellationToken cancellationToken = default);

    Task EnsureTargetProviderCanBeLinkedAsync(
        string provider,
        string providerSubject,
        Guid currentUserId,
        IReadOnlyList<string> linkedProviders,
        CancellationToken cancellationToken = default);
}
