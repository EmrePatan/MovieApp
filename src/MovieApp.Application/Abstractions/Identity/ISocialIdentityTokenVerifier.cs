using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Abstractions.Identity;

public interface ISocialIdentityTokenVerifier
{
    string Provider { get; }

    Task<VerifiedSocialIdentity> VerifyIdentityTokenAsync(
        string identityToken,
        CancellationToken cancellationToken = default);
}
