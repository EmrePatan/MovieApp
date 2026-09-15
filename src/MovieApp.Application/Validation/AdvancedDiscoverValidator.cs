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
    public const int MaximumWatchProviders = 20;
    public const int MinimumWatchProviderId = 1;
    public const int MaximumWatchProviderId = 1_000_000;

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

        var watchRegionValidation = ValidateWatchRegion(criteria.WatchRegion, required: false);
        if (!watchRegionValidation.IsValid)
        {
            return watchRegionValidation;
        }

        var watchProviderValidation = ValidateWatchProviderIds(criteria.WatchProviderIds);
        if (!watchProviderValidation.IsValid)
        {
            return watchProviderValidation;
        }

        var watchMonetizationValidation = ValidateWatchMonetizationTypes(criteria.WatchMonetizationTypes);
        if (!watchMonetizationValidation.IsValid)
        {
            return watchMonetizationValidation;
        }

        var watchFilterValidation = ValidateWatchFilterCombination(
            criteria.WatchRegion,
            criteria.WatchProviderIds,
            criteria.WatchMonetizationTypes);
        if (!watchFilterValidation.IsValid)
        {
            return watchFilterValidation;
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

    public static SearchQueryValidationResult ValidateWatchRegion(string? watchRegion, bool required)
    {
        if (string.IsNullOrWhiteSpace(watchRegion))
        {
            return required
                ? SearchQueryValidationResult.Failure("Watch region is required.")
                : SearchQueryValidationResult.Success();
        }

        return WatchProviderRegionValidator.Validate(watchRegion);
    }

    public static SearchQueryValidationResult ValidateWatchProviderIds(IReadOnlyList<int> watchProviderIds)
    {
        if (watchProviderIds.Count == 0)
        {
            return SearchQueryValidationResult.Success();
        }

        if (watchProviderIds.Count > MaximumWatchProviders)
        {
            return SearchQueryValidationResult.Failure(
                $"A maximum of {MaximumWatchProviders} watch providers is supported.");
        }

        if (watchProviderIds.Any(id => id < MinimumWatchProviderId || id > MaximumWatchProviderId))
        {
            return SearchQueryValidationResult.Failure("Watch provider IDs are invalid.");
        }

        if (watchProviderIds.Distinct().Count() != watchProviderIds.Count)
        {
            return SearchQueryValidationResult.Failure("Duplicate watch provider IDs are not allowed.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateWatchMonetizationTypes(
        IReadOnlyList<WatchMonetizationType> watchMonetizationTypes)
    {
        if (watchMonetizationTypes.Count == 0)
        {
            return SearchQueryValidationResult.Success();
        }

        if (watchMonetizationTypes.Any(type => !Enum.IsDefined(type)))
        {
            return SearchQueryValidationResult.Failure("Watch availability type is not supported.");
        }

        if (watchMonetizationTypes.Distinct().Count() != watchMonetizationTypes.Count)
        {
            return SearchQueryValidationResult.Failure("Duplicate watch availability types are not allowed.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateWatchFilterCombination(
        string? watchRegion,
        IReadOnlyList<int> watchProviderIds,
        IReadOnlyList<WatchMonetizationType> watchMonetizationTypes)
    {
        var hasProviders = watchProviderIds.Count > 0;
        var hasMonetization = watchMonetizationTypes.Count > 0;
        var hasRegion = !string.IsNullOrWhiteSpace(watchRegion);

        if ((hasProviders || hasMonetization) && !hasRegion)
        {
            return SearchQueryValidationResult.Failure(
                "Watch region is required when filtering by streaming providers or availability.");
        }

        if (!hasProviders && !hasMonetization)
        {
            return SearchQueryValidationResult.Success();
        }

        return SearchQueryValidationResult.Success();
    }

    public static IReadOnlyList<int> ParseWatchProviderIds(IEnumerable<string>? providerIdValues)
    {
        if (providerIdValues is null)
        {
            return [];
        }

        var ids = new List<int>();

        foreach (var rawValue in providerIdValues)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            foreach (var segment in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(segment, out var providerId))
                {
                    continue;
                }

                ids.Add(providerId);
            }
        }

        return ids.Distinct().ToList();
    }

    public static SearchQueryValidationResult ValidateWatchMonetizationTypeValues(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return SearchQueryValidationResult.Success();
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!TryParseWatchMonetizationType(value, out _))
            {
                return SearchQueryValidationResult.Failure(
                    "Watch availability type must be one of: stream, free, ads, rent, buy.");
            }
        }

        return SearchQueryValidationResult.Success();
    }

    public static IReadOnlyList<WatchMonetizationType> ParseWatchMonetizationTypes(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        var types = new List<WatchMonetizationType>();

        foreach (var rawValue in values)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            foreach (var segment in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (TryParseWatchMonetizationType(segment, out var monetizationType))
                {
                    types.Add(monetizationType);
                }
            }
        }

        return types.Distinct().ToList();
    }

    public static bool TryParseWatchMonetizationType(
        string? value,
        out WatchMonetizationType monetizationType)
    {
        monetizationType = WatchMonetizationType.Stream;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "stream":
            case "flatrate":
                monetizationType = WatchMonetizationType.Stream;
                return true;
            case "free":
                monetizationType = WatchMonetizationType.Free;
                return true;
            case "ads":
                monetizationType = WatchMonetizationType.Ads;
                return true;
            case "rent":
                monetizationType = WatchMonetizationType.Rent;
                return true;
            case "buy":
                monetizationType = WatchMonetizationType.Buy;
                return true;
            default:
                return false;
        }
    }
}
