using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbDiscoverQueryBuilder
{
    public const int TopRatedMinimumVoteCount = DiscoverCatalogSortPolicy.TopRatedMinimumVoteCount;

    public static string BuildMovieQuery(
        DiscoverProviderCriteria criteria,
        NewReleasesDiscoverConstraints? newReleases = null)
    {
        var constraints = newReleases ?? NewReleasesDiscoverConstraints.ForMovies(null);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapMovieSort(criteria)}"
        };

        AppendSharedFilters(parameters, criteria, includeTvStatuses: false, constraints);
        AppendMovieYearFilters(parameters, criteria, constraints, today);

        return string.Join('&', parameters);
    }

    public static string BuildTvQuery(
        DiscoverProviderCriteria criteria,
        NewReleasesDiscoverConstraints? newReleases = null)
    {
        var constraints = newReleases ?? NewReleasesDiscoverConstraints.ForTvShows(null);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapTvSort(criteria)}"
        };

        AppendSharedFilters(parameters, criteria, includeTvStatuses: true, constraints);
        AppendTvYearFilters(parameters, criteria, constraints, today);

        return string.Join('&', parameters);
    }

    private static void AppendSharedFilters(
        List<string> parameters,
        DiscoverProviderCriteria criteria,
        bool includeTvStatuses,
        NewReleasesDiscoverConstraints newReleases)
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

        var effectiveMinVoteCount = ResolveEffectiveMinVoteCount(criteria, newReleases);
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

    private static int? ResolveEffectiveMinVoteCount(
        DiscoverProviderCriteria criteria,
        NewReleasesDiscoverConstraints newReleases)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);
        int? newReleasesFloor = criteria.Mode == DiscoverBrowseMode.NewReleases && newReleases.MinVoteCount > 0
            ? newReleases.MinVoteCount
            : null;

        return DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            criteria.Mode,
            effectiveSort,
            criteria.MinVoteCount,
            newReleasesFloor);
    }

    private static void AppendMovieYearFilters(
        List<string> parameters,
        DiscoverProviderCriteria criteria,
        NewReleasesDiscoverConstraints newReleases,
        DateOnly today)
    {
        if (criteria.Year.HasValue)
        {
            parameters.Add($"primary_release_year={criteria.Year.Value}");
            if (criteria.Mode == DiscoverBrowseMode.NewReleases)
            {
                parameters.Add($"primary_release_date.lte={today:yyyy-MM-dd}");
            }

            return;
        }

        if (criteria.YearFrom.HasValue)
        {
            parameters.Add($"primary_release_date.gte={criteria.YearFrom.Value:0000}-01-01");
        }
        else if (ShouldApplyNewReleasesRecentWindow(criteria, newReleases.MaxAgeDays))
        {
            parameters.Add($"primary_release_date.gte={today.AddDays(-newReleases.MaxAgeDays):yyyy-MM-dd}");
        }

        var upperBound = ResolveMovieReleaseUpperBound(criteria, today);
        if (upperBound.HasValue)
        {
            parameters.Add($"primary_release_date.lte={upperBound.Value:yyyy-MM-dd}");
        }
    }

    private static void AppendTvYearFilters(
        List<string> parameters,
        DiscoverProviderCriteria criteria,
        NewReleasesDiscoverConstraints newReleases,
        DateOnly today)
    {
        if (criteria.Year.HasValue)
        {
            parameters.Add($"first_air_date_year={criteria.Year.Value}");
            if (criteria.Mode == DiscoverBrowseMode.NewReleases)
            {
                parameters.Add($"first_air_date.lte={today:yyyy-MM-dd}");
            }

            return;
        }

        if (criteria.YearFrom.HasValue)
        {
            parameters.Add($"first_air_date.gte={criteria.YearFrom.Value:0000}-01-01");
        }
        else if (ShouldApplyNewReleasesRecentWindow(criteria, newReleases.MaxAgeDays))
        {
            parameters.Add($"first_air_date.gte={today.AddDays(-newReleases.MaxAgeDays):yyyy-MM-dd}");
        }

        var upperBound = ResolveTvReleaseUpperBound(criteria, today);
        if (upperBound.HasValue)
        {
            parameters.Add($"first_air_date.lte={upperBound.Value:yyyy-MM-dd}");
        }
    }

    private static bool ShouldApplyNewReleasesRecentWindow(
        DiscoverProviderCriteria criteria,
        int maxAgeDays) =>
        criteria.Mode == DiscoverBrowseMode.NewReleases
        && maxAgeDays > 0
        && !criteria.Year.HasValue
        && !criteria.YearFrom.HasValue
        && !criteria.YearTo.HasValue;

    private static DateOnly? ResolveMovieReleaseUpperBound(DiscoverProviderCriteria criteria, DateOnly today)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);
        return ResolveReleaseUpperBound(criteria, effectiveSort, today);
    }

    private static DateOnly? ResolveTvReleaseUpperBound(DiscoverProviderCriteria criteria, DateOnly today)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);
        return ResolveReleaseUpperBound(criteria, effectiveSort, today);
    }

    private static DateOnly? ResolveReleaseUpperBound(
        DiscoverProviderCriteria criteria,
        DiscoverBrowseSort effectiveSort,
        DateOnly today)
    {
        if (criteria.Year.HasValue)
        {
            if (criteria.Mode == DiscoverBrowseMode.NewReleases)
            {
                return today;
            }

            return null;
        }

        DateOnly? userUpper = criteria.YearTo.HasValue
            ? new DateOnly(criteria.YearTo.Value, 12, 31)
            : null;

        DateOnly? newReleasesCap = criteria.Mode == DiscoverBrowseMode.NewReleases
            ? today
            : null;

        DateOnly? futureCap = DiscoverCatalogSortPolicy.RequiresFutureReleaseCap(effectiveSort)
            ? today.AddDays(DiscoverCatalogSortPolicy.FutureReleaseHorizonDays)
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
