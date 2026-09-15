using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Validation;

public static class SocialAuthValidator
{
    public static SearchQueryValidationResult Validate(SocialAuthRequest request)
    {
        if (request is null)
        {
            return SearchQueryValidationResult.Failure("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            return SearchQueryValidationResult.Failure("Provider is required.");
        }

        if (!ExternalLoginProviders.IsSupported(request.Provider))
        {
            return SearchQueryValidationResult.Failure("Unsupported social provider.");
        }

        if (string.IsNullOrWhiteSpace(request.IdentityToken))
        {
            return SearchQueryValidationResult.Failure("Identity token is required.");
        }

        return SearchQueryValidationResult.Success();
    }
}
