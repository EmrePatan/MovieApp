using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

internal static class ExternalLoginPictureSync
{
    public static async Task RefreshGooglePictureIfPresentAsync(
        IUserExternalLoginRepository externalLoginRepository,
        Guid userId,
        VerifiedSocialIdentity identity,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(identity.Provider, ExternalLoginProviders.Google, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(identity.ProviderPictureUrl))
        {
            return;
        }

        await externalLoginRepository.UpdateProviderPictureUrlAsync(
            userId,
            ExternalLoginProviders.Google,
            identity.ProviderPictureUrl,
            cancellationToken);
    }
}
