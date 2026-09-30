using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbDiscoverQueryBuilder
{
    public const int TopRatedMinimumVoteCount = DiscoverCatalogSortPolicy.TopRatedMinimumVoteCount;

    public static string BuildMovieQuery(DiscoverProviderCriteria criteria)
    {
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapMovieSort(criteria)}"
        };

        AppendSharedFilters(parameters, criteria, includeTvStatuses: false);
        AppendMovieYearFilters(parameters, criteria);

        return string.Join('&', parameters);
    }

    public static string BuildTvQuery(DiscoverProviderCriteria criteria)
    {
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapTvSort(criteria)}"
        };

        AppendSharedFilters(parameters, criteria, includeTvStatuses: true);
        AppendTvYearFilters(parameters, criteria);

        return string.Join('&', parameters);
    }

    private static void AppendSharedFilters(
        List<string> parameters,
        DiscoverProviderCriteria criteria,
        bool includeTvStatuses)
    {
        parameters.Add("include_adult=false");

        if (criteria.GenreTmdbIds.Count > 0)
        {
            parameters.Add($"with_genres={string.Join(',', criteria.GenreTmdbIds)}");
        }

        if (criteria.MinRating.HasValue)
        {
            parameters.Add($"vote_average.gte={criteria.MinRating.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var effectiveMinVoteCount = ResolveEffectiveMinVoteCount(criteria);
        if (effectiveMinVoteCount.HasValue)
        {
            parameters.Add($"vote_count.gte={effectiveMinVoteCount.Value}");
        }

        if (criteria.MinRuntimeMinutes.HasValue)
        {
            parameters.Add($"with_runtime.gte={criteria.MinRuntimeMinutes.Value}");
        }

        if (criteria.MaxRuntimeMinutes.HasValue)
        {
            parameters.Add($"with_runtime.lte={criteria.MaxRuntimeMinutes.Value}");
        }

        if (!string.IsNullOrWhiteSpace(criteria.Language))
        {
            parameters.Add($"with_original_language={Uri.EscapeDataString(criteria.Language.Trim().ToLowerInvariant())}");
        }

        if (!string.IsNullOrWhiteSpace(criteria.OriginCountry))
        {
            parameters.Add(
                $"with_origin_country={Uri.EscapeDataString(criteria.OriginCountry.Trim().ToUpperInvariant())}");
        }

        if (criteria.KeywordTmdbIds.Count > 0)
        {
            parameters.Add($"with_keywords={string.Join('|', criteria.KeywordTmdbIds)}");
        }

        if (includeTvStatuses && criteria.TvStatuses.Count > 0)
        {
            var statusIds = criteria.TvStatuses
                .Distinct()
                .OrderBy(status => (int)status)
                .Select(status => ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture));

            parameters.Add($"with_status={string.Join('|', statusIds)}");
        }
    }

    private static int? ResolveEffectiveMinVoteCount(DiscoverProviderCriteria criteria)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);
        return DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            criteria.Mode,
            effectiveSort,
            criteria.MinVoteCount);
    }

    private static void AppendMovieYearFilters(List<string> parameters, DiscoverProviderCriteria criteria)
    {
        if (criteria.Year.HasValue)
        {
            parameters.Add($"primary_release_year={criteria.Year.Value}");
            if (criteria.Mode == DiscoverBrowseMode.NewReleases)
            {
                parameters.Add($"primary_release_date.lte={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
            }

            return;
        }

        if (criteria.YearFrom.HasValue)
        {
            parameters.Add($"primary_release_date.gte={criteria.YearFrom.Value:0000}-01-01");
        }

        var upperBound = ResolveMovieReleaseUpperBound(criteria);
        if (upperBound.HasValue)
        {
            parameters.Add($"primary_release_date.lte={upperBound.Value:yyyy-MM-dd}");
        }
    }

    private static void AppendTvYearFilters(List<string> parameters, DiscoverProviderCriteria criteria)
    {
        if (criteria.Year.HasValue)
        {
            parameters.Add($"first_air_date_year={criteria.Year.Value}");
            if (criteria.Mode == DiscoverBrowseMode.NewReleases)
            {
                parameters.Add($"first_air_date.lte={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
            }

            return;
        }

        if (criteria.YearFrom.HasValue)
        {
            parameters.Add($"first_air_date.gte={criteria.YearFrom.Value:0000}-01-01");
        }

        var upperBound = ResolveTvReleaseUpperBound(criteria);
        if (upperBound.HasValue)
        {
            parameters.Add($"first_air_date.lte={upperBound.Value:yyyy-MM-dd}");
        }
    }

    private static DateOnly? ResolveMovieReleaseUpperBound(DiscoverProviderCriteria criteria)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);
        return ResolveReleaseUpperBound(criteria, effectiveSort, isMovie: true);
    }

    private static DateOnly? ResolveTvReleaseUpperBound(DiscoverProviderCriteria criteria)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);
        return ResolveReleaseUpperBound(criteria, effectiveSort, isMovie: false);
    }

    private static DateOnly? ResolveReleaseUpperBound(
        DiscoverProviderCriteria criteria,
        DiscoverBrowseSort effectiveSort,
        bool isMovie)
    {
        if (criteria.Year.HasValue)
        {
            if (criteria.Mode == DiscoverBrowseMode.NewReleases)
            {
                return DateOnly.FromDateTime(DateTime.UtcNow);
            }

            return null;
        }

        DateOnly? userUpper = criteria.YearTo.HasValue
            ? new DateOnly(criteria.YearTo.Value, 12, 31)
            : null;

        DateOnly? newReleasesCap = criteria.Mode == DiscoverBrowseMode.NewReleases
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : null;

        DateOnly? futureCap = DiscoverCatalogSortPolicy.RequiresFutureReleaseCap(effectiveSort)
            ? DiscoverCatalogSortPolicy.GetFutureReleaseCutoffDate(DateTime.UtcNow)
            : null;

        return DiscoverCatalogSortPolicy.ResolveReleaseUpperBound(userUpper, futureCap, newReleasesCap);
    }

    private static string MapMovieSort(DiscoverProviderCriteria criteria)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);

        return effectiveSort switch
        {
            DiscoverBrowseSort.PopularityAsc => "popularity.asc",
            DiscoverBrowseSort.RatingDesc => "vote_average.desc",
            DiscoverBrowseSort.RatingAsc => "vote_average.asc",
            DiscoverBrowseSort.ReleaseDesc => "primary_release_date.desc",
            DiscoverBrowseSort.ReleaseAsc => "primary_release_date.asc",
            DiscoverBrowseSort.TitleAsc => "original_title.asc",
            DiscoverBrowseSort.TitleDesc => "original_title.desc",
            _ => "popularity.desc"
        };
    }

    private static string MapTvSort(DiscoverProviderCriteria criteria)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);

        return effectiveSort switch
        {
            DiscoverBrowseSort.PopularityAsc => "popularity.asc",
            DiscoverBrowseSort.RatingDesc => "vote_average.desc",
            DiscoverBrowseSort.RatingAsc => "vote_average.asc",
            DiscoverBrowseSort.ReleaseDesc => "first_air_date.desc",
            DiscoverBrowseSort.ReleaseAsc => "first_air_date.asc",
            DiscoverBrowseSort.TitleAsc => "original_title.asc",
            DiscoverBrowseSort.TitleDesc => "original_title.desc",
            _ => "popularity.desc"
        };
    }
}
