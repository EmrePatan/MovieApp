using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Validation;

public static class WorldCinemaValidator
{
    public static SearchQueryValidationResult Validate(WorldCinemaCriteria criteria)
    {
        var mediaTypeValidation = AdvancedDiscoverValidator.ValidateMediaType(criteria.MediaType);
        if (!mediaTypeValidation.IsValid)
        {
            return mediaTypeValidation;
        }

        var originCountryValidation = ValidateRequiredOriginCountry(criteria.OriginCountry);
        if (!originCountryValidation.IsValid)
        {
            return originCountryValidation;
        }

        var paginationValidation = AdvancedSearchValidator.ValidatePagination(criteria.Page, criteria.PageSize);
        if (!paginationValidation.IsValid)
        {
            return paginationValidation;
        }

        var yearValidation = AdvancedDiscoverValidator.ValidateYearSelection(null, criteria.YearFrom, criteria.YearTo);
        if (!yearValidation.IsValid)
        {
            return yearValidation;
        }

        var ratingValidation = AdvancedSearchValidator.ValidateRatings(criteria.MinRating, criteria.MaxRating);
        if (!ratingValidation.IsValid)
        {
            return ratingValidation;
        }

        var voteCountValidation = AdvancedDiscoverValidator.ValidateVoteCount(criteria.MinVoteCount);
        if (!voteCountValidation.IsValid)
        {
            return voteCountValidation;
        }

        var runtimeValidation = AdvancedDiscoverValidator.ValidateRuntime(
            criteria.MinRuntimeMinutes,
            criteria.MaxRuntimeMinutes);
        if (!runtimeValidation.IsValid)
        {
            return runtimeValidation;
        }

        var languageValidation = DiscoverBrowseValidator.ValidateLanguage(criteria.OriginalLanguage);
        if (!languageValidation.IsValid)
        {
            return languageValidation;
        }

        var genreMatchValidation = AdvancedDiscoverValidator.ValidateGenreMatch(criteria.GenreMatch);
        if (!genreMatchValidation.IsValid)
        {
            return genreMatchValidation;
        }

        var keywordValidation = AdvancedDiscoverValidator.ValidateKeywordIds(criteria.KeywordIds);
        if (!keywordValidation.IsValid)
        {
            return keywordValidation;
        }

        var tvStatusValidation = AdvancedDiscoverValidator.ValidateTvStatuses(
            criteria.MediaType,
            criteria.TvStatuses);
        if (!tvStatusValidation.IsValid)
        {
            return tvStatusValidation;
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateRequiredOriginCountry(string? originCountry)
    {
        if (string.IsNullOrWhiteSpace(originCountry))
        {
            return SearchQueryValidationResult.Failure("Origin country is required.");
        }

        return AdvancedDiscoverValidator.ValidateOriginCountry(originCountry);
    }

    public static SearchQueryValidationResult ValidateMediaTypeValue(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return SearchQueryValidationResult.Failure("Media type is required.");
        }

        return AdvancedDiscoverValidator.ValidateMediaType(mediaType);
    }

    public static SearchQueryValidationResult ValidateSortValue(string? sort) =>
        AdvancedDiscoverValidator.ValidateSort(sort);
}
