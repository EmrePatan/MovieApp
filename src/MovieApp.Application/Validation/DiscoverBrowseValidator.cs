using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Validation;

public static class DiscoverBrowseValidator
{
    public const int MinimumLanguageLength = 2;
    public const int MaximumLanguageLength = 5;

    public static SearchQueryValidationResult Validate(DiscoverBrowseCriteria criteria)
    {
        var modeValidation = ValidateMode(criteria.Mode);
        if (!modeValidation.IsValid)
        {
            return modeValidation;
        }

        var paginationValidation = AdvancedSearchValidator.ValidatePagination(criteria.Page, criteria.PageSize);
        if (!paginationValidation.IsValid)
        {
            return paginationValidation;
        }

        var yearValidation = AdvancedSearchValidator.ValidateYear(criteria.Year);
        if (!yearValidation.IsValid)
        {
            return yearValidation;
        }

        var ratingValidation = AdvancedSearchValidator.ValidateRatings(criteria.MinRating, null);
        if (!ratingValidation.IsValid)
        {
            return ratingValidation;
        }

        var languageValidation = ValidateLanguage(criteria.Language);
        if (!languageValidation.IsValid)
        {
            return languageValidation;
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateMode(DiscoverBrowseMode mode) =>
        Enum.IsDefined(mode)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Mode must be one of: trending, top_rated, new_releases.");

    public static SearchQueryValidationResult ValidateMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return SearchQueryValidationResult.Failure("Mode is required.");
        }

        return TryParseMode(mode, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Mode must be one of: trending, top_rated, new_releases.");
    }

    public static SearchQueryValidationResult ValidateSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return SearchQueryValidationResult.Success();
        }

        return TryParseSort(sort, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure(
                "Sort must be one of: popularity_desc, popularity_asc, rating_desc, rating_asc, release_desc, release_asc, title_asc, title_desc.");
    }

    public static SearchQueryValidationResult ValidateLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return SearchQueryValidationResult.Success();
        }

        var trimmed = language.Trim();
        if (trimmed.Length < MinimumLanguageLength || trimmed.Length > MaximumLanguageLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Language must be between {MinimumLanguageLength} and {MaximumLanguageLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static bool TryParseMode(string? mode, out DiscoverBrowseMode browseMode)
    {
        browseMode = DiscoverBrowseMode.Trending;

        if (string.IsNullOrWhiteSpace(mode))
        {
            return false;
        }

        switch (mode.Trim().ToLowerInvariant())
        {
            case "trending":
                browseMode = DiscoverBrowseMode.Trending;
                return true;
            case "top_rated":
                browseMode = DiscoverBrowseMode.TopRated;
                return true;
            case "new_releases":
                browseMode = DiscoverBrowseMode.NewReleases;
                return true;
            default:
                return false;
        }
    }

    public static bool TryParseSort(string? sort, out DiscoverBrowseSort? browseSort)
    {
        browseSort = null;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        switch (sort.Trim().ToLowerInvariant())
        {
            case "popularity_desc":
                browseSort = DiscoverBrowseSort.PopularityDesc;
                return true;
            case "popularity_asc":
                browseSort = DiscoverBrowseSort.PopularityAsc;
                return true;
            case "rating_desc":
                browseSort = DiscoverBrowseSort.RatingDesc;
                return true;
            case "rating_asc":
                browseSort = DiscoverBrowseSort.RatingAsc;
                return true;
            case "release_desc":
                browseSort = DiscoverBrowseSort.ReleaseDesc;
                return true;
            case "release_asc":
                browseSort = DiscoverBrowseSort.ReleaseAsc;
                return true;
            case "title_asc":
                browseSort = DiscoverBrowseSort.TitleAsc;
                return true;
            case "title_desc":
                browseSort = DiscoverBrowseSort.TitleDesc;
                return true;
            default:
                return false;
        }
    }

    public static DiscoverBrowseSort GetDefaultSortForMode(DiscoverBrowseMode mode) =>
        mode switch
        {
            DiscoverBrowseMode.TopRated => DiscoverBrowseSort.RatingDesc,
            DiscoverBrowseMode.NewReleases => DiscoverBrowseSort.ReleaseDesc,
            _ => DiscoverBrowseSort.PopularityDesc
        };

    public static DiscoverBrowseSort GetEffectiveSort(DiscoverBrowseCriteria criteria) =>
        criteria.Sort ?? GetDefaultSortForMode(criteria.Mode);

    public static IReadOnlyList<Guid> ParseGenreIds(IEnumerable<string>? genreIdValues, IEnumerable<Guid>? genreGuids)
    {
        var parsed = new HashSet<Guid>();

        if (genreGuids is not null)
        {
            foreach (var genreId in genreGuids)
            {
                if (genreId != Guid.Empty)
                {
                    parsed.Add(genreId);
                }
            }
        }

        if (genreIdValues is not null)
        {
            foreach (var rawValue in genreIdValues)
            {
                foreach (var segment in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Guid.TryParse(segment, out var genreId) && genreId != Guid.Empty)
                    {
                        parsed.Add(genreId);
                    }
                }
            }
        }

        return parsed.OrderBy(id => id).ToList();
    }
}
