using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Discovery;

namespace MovieApp.Application.Validation;

public static class NowInTheatersValidator
{
    public static SearchQueryValidationResult ValidateReleaseRegion(string? releaseRegion, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(releaseRegion))
        {
            return required
                ? SearchQueryValidationResult.Failure("Release region is required.")
                : SearchQueryValidationResult.Success();
        }

        return WatchProviderRegionValidator.Validate(releaseRegion);
    }

    public static SearchQueryValidationResult Validate(NowInTheatersCriteria criteria)
    {
        var regionValidation = ValidateReleaseRegion(criteria.ReleaseRegion, required: true);
        if (!regionValidation.IsValid)
        {
            return regionValidation;
        }

        if (criteria.Page < SearchPaginationDefaults.MinPage)
        {
            return SearchQueryValidationResult.Failure("Page must be at least 1.");
        }

        if (criteria.PageSize < 1 || criteria.PageSize > SearchPaginationDefaults.MaxPageSize)
        {
            return SearchQueryValidationResult.Failure(
                $"Page size must be between 1 and {SearchPaginationDefaults.MaxPageSize}.");
        }

        return SearchQueryValidationResult.Success();
    }
}
