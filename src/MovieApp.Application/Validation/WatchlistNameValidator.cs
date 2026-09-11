using MovieApp.Application.Models.Watchlists;
using MovieApp.Domain.Watchlists;

namespace MovieApp.Application.Validation;

public static class WatchlistNameValidator
{
    public static SearchQueryValidationResult Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SearchQueryValidationResult.Failure("Watchlist name is required.");
        }

        if (name.Trim().Length > WatchlistNameNormalizer.MaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Watchlist name must not exceed {WatchlistNameNormalizer.MaxLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }
}
