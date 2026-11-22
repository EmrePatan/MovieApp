using MovieApp.Domain.Entities;

namespace MovieApp.Application.Validation;

public static class WatchlistDescriptionValidator
{
    public static SearchQueryValidationResult Validate(string? description)
    {
        if (description is not null && description.Trim().Length > Watchlist.MaxDescriptionLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Watchlist description must not exceed {Watchlist.MaxDescriptionLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }
}