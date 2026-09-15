using System.Text.RegularExpressions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Validation;

public static class AdvancedDiscoverValidator
{
    public const int MinimumRuntimeMinutes = 0;
    public const int MaximumRuntimeMinutes = 400;
    public const int MinimumVoteCount = 0;
    public const int MaximumVoteCount = 100_000;
    public const int OriginCountryCodeLength = 2;

    private static readonly Regex OriginCountryPattern = new(
        "^[A-Za-z]{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static SearchQueryValidationResult Validate(AdvancedDiscoverCriteria criteria)
    {
        var mediaTypeValidation = ValidateMediaType(criteria.MediaType);
        if (!mediaTypeValidation.IsValid)
        {
            return mediaTypeValidation;
        }

        var paginationValidation = AdvancedSearchValidator.ValidatePagination(criteria.Page, criteria.PageSize);
        if (!paginationValidation.IsValid)
        {
            return paginationValidation;
        }

        var yearValidation = ValidateYearSelection(criteria.Year, criteria.YearFrom, criteria.YearTo);
        if (!yearValidation.IsValid)
        {
            return yearValidation;
        }

        var ratingValidation = AdvancedSearchValidator.ValidateRatings(criteria.MinRating, criteria.MaxRating);
        if (!ratingValidation.IsValid)
        {
            return ratingValidation;
        }

        var voteCountValidation = ValidateVoteCount(criteria.MinVoteCount);
        if (!voteCountValidation.IsValid)
        {
            return voteCountValidation;
        }

        var runtimeValidation = ValidateRuntime(criteria.MinRuntimeMinutes, criteria.MaxRuntimeMinutes);
        if (!runtimeValidation.IsValid)
        {
            return runtimeValidation;
        }

        var languageValidation = DiscoverBrowseValidator.ValidateLanguage(criteria.OriginalLanguage);
        if (!languageValidation.IsValid)
        {
            return languageValidation;
        }

        var originCountryValidation = ValidateOriginCountry(criteria.OriginCountry);
        if (!originCountryValidation.IsValid)
        {
            return originCountryValidation;
        }

        return Enum.IsDefined(criteria.Sort)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Sort is not supported.");
    }

    public static SearchQueryValidationResult ValidateMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return SearchQueryValidationResult.Failure("Media type is required.");
        }

        if (!AdvancedSearchValidator.TryParseType(mediaType, out var contentType))
        {
            return SearchQueryValidationResult.Failure("Media type must be movie or tv.");
        }

        return ValidateMediaType(contentType);
    }

    public static SearchQueryValidationResult ValidateMediaType(SearchContentType mediaType) =>
        mediaType is SearchContentType.Movie or SearchContentType.Tv
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Media type must be movie or tv.");

    public static SearchQueryValidationResult ValidateSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return SearchQueryValidationResult.Success();
        }

        return TryParseSort(sort, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure(
                "Sort must be one of: popularity_desc, rating_desc, newest, oldest.");
    }

    public static SearchQueryValidationResult ValidateOriginCountry(string? originCountry)
    {
        if (string.IsNullOrWhiteSpace(originCountry))
        {
            return SearchQueryValidationResult.Success();
        }

        var trimmed = originCountry.Trim();
        if (!OriginCountryPattern.IsMatch(trimmed))
        {
            return SearchQueryValidationResult.Failure(
                "Origin country must be a 2-letter ISO country code.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateYearSelection(int? year, int? yearFrom, int? yearTo)
    {
        if (year.HasValue && (yearFrom.HasValue || yearTo.HasValue))
        {
            return SearchQueryValidationResult.Failure(
                "Specify either year or a year range, not both.");
        }

        var yearValidation = AdvancedSearchValidator.ValidateYear(year);
        if (!yearValidation.IsValid)
        {
            return yearValidation;
        }

        var yearFromValidation = AdvancedSearchValidator.ValidateYear(yearFrom);
        if (!yearFromValidation.IsValid)
        {
            return yearFromValidation;
        }

        var yearToValidation = AdvancedSearchValidator.ValidateYear(yearTo);
        if (!yearToValidation.IsValid)
        {
            return yearToValidation;
        }

        if (yearFrom.HasValue && yearTo.HasValue && yearFrom > yearTo)
        {
            return SearchQueryValidationResult.Failure("Year from must not exceed year to.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateVoteCount(int? minVoteCount)
    {
        if (minVoteCount is null)
        {
            return SearchQueryValidationResult.Success();
        }

        if (minVoteCount < MinimumVoteCount || minVoteCount > MaximumVoteCount)
        {
            return SearchQueryValidationResult.Failure(
                $"Minimum vote count must be between {MinimumVoteCount} and {MaximumVoteCount}.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateRuntime(int? minRuntimeMinutes, int? maxRuntimeMinutes)
    {
        if (minRuntimeMinutes is < MinimumRuntimeMinutes or > MaximumRuntimeMinutes)
        {
            return SearchQueryValidationResult.Failure(
                $"Minimum runtime must be between {MinimumRuntimeMinutes} and {MaximumRuntimeMinutes} minutes.");
        }

        if (maxRuntimeMinutes is < MinimumRuntimeMinutes or > MaximumRuntimeMinutes)
        {
            return SearchQueryValidationResult.Failure(
                $"Maximum runtime must be between {MinimumRuntimeMinutes} and {MaximumRuntimeMinutes} minutes.");
        }

        if (minRuntimeMinutes is not null &&
            maxRuntimeMinutes is not null &&
            minRuntimeMinutes > maxRuntimeMinutes)
        {
            return SearchQueryValidationResult.Failure("Minimum runtime must not exceed maximum runtime.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static bool TryParseSort(string? sort, out AdvancedDiscoverSort discoverSort)
    {
        discoverSort = AdvancedDiscoverSort.PopularityDesc;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        switch (sort.Trim().ToLowerInvariant())
        {
            case "popularity_desc":
                discoverSort = AdvancedDiscoverSort.PopularityDesc;
                return true;
            case "rating_desc":
                discoverSort = AdvancedDiscoverSort.RatingDesc;
                return true;
            case "newest":
                discoverSort = AdvancedDiscoverSort.Newest;
                return true;
            case "oldest":
                discoverSort = AdvancedDiscoverSort.Oldest;
                return true;
            default:
                return false;
        }
    }

    public static IReadOnlyList<Guid> ParseGenreIds(IEnumerable<string>? genreIdValues) =>
        DiscoverBrowseValidator.ParseGenreIds(genreIdValues, null);
}
