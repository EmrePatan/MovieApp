using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbAdvancedDiscoverQueryBuilder
{
    public static string BuildMovieQuery(
        AdvancedDiscoverProviderCriteria criteria,
        int minVoteCountWhenWatchProvider = AdvancedDiscoverOptions.DefaultMinVoteCountWhenWatchProvider)
    {
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapMovieSort(criteria.Sort)}",
            "include_adult=false"
        };

        AppendSharedFilters(parameters, criteria, minVoteCountWhenWatchProvider);
        AppendMovieYearFilters(parameters, criteria);
        AppendMovieOnlyFilters(parameters, criteria);
        AppendKeywords(parameters, criteria.KeywordTmdbIds);

        return string.Join('&', parameters);
    }

    public static string BuildTvQuery(
        AdvancedDiscoverProviderCriteria criteria,
        int minVoteCountWhenWatchProvider = AdvancedDiscoverOptions.DefaultMinVoteCountWhenWatchProvider)
    {
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapTvSort(criteria.Sort)}",
            "include_adult=false"
        };

        AppendSharedFilters(parameters, criteria, minVoteCountWhenWatchProvider);
        AppendTvYearFilters(parameters, criteria);
        AppendKeywords(parameters, criteria.KeywordTmdbIds);
        AppendTvStatuses(parameters, criteria.TvStatuses);

        return string.Join('&', parameters);
    }

    private static void AppendSharedFilters(
        List<string> parameters,
        AdvancedDiscoverProviderCriteria criteria,
        int minVoteCountWhenWatchProvider)
    {
        if (criteria.GenreTmdbIds.Count > 0)
        {
            var genreDelimiter = criteria.GenreMatch == GenreMatchMode.Any ? '|' : ',';
            parameters.Add(
                $"with_genres={string.Join(genreDelimiter, criteria.GenreTmdbIds)}");
        }

        if (criteria.MinRating.HasValue)
        {
            parameters.Add(
                $"vote_average.gte={criteria.MinRating.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (criteria.MaxRating.HasValue)
        {
            parameters.Add(
                $"vote_average.lte={criteria.MaxRating.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var effectiveMinVoteCount = AdvancedDiscoverVoteCountPolicy.ResolveMinVoteCount(
            criteria.MinVoteCount,
            criteria.WatchProviderIds.Count > 0,
            minVoteCountWhenWatchProvider);

        effectiveMinVoteCount = DiscoverCatalogSortPolicy.ResolveAdvancedMinVoteCount(
            criteria.Sort,
            effectiveMinVoteCount,
            null);

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

        if (!string.IsNullOrWhiteSpace(criteria.OriginalLanguage))
        {
            parameters.Add(
                $"with_original_language={Uri.EscapeDataString(criteria.OriginalLanguage.Trim().ToLowerInvariant())}");
        }

        if (!string.IsNullOrWhiteSpace(criteria.OriginCountry))
        {
            parameters.Add(
                $"with_origin_country={Uri.EscapeDataString(criteria.OriginCountry.Trim().ToUpperInvariant())}");
        }

        AppendWatchFilters(parameters, criteria);
    }

    private static void AppendWatchFilters(List<string> parameters, AdvancedDiscoverProviderCriteria criteria)
    {
        var hasProviders = criteria.WatchProviderIds.Count > 0;
        var hasMonetization = criteria.WatchMonetizationTypes.Count > 0;

        if (!hasProviders && !hasMonetization)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(criteria.WatchRegion))
        {
            return;
        }

        parameters.Add(
            $"watch_region={Uri.EscapeDataString(criteria.WatchRegion.Trim().ToUpperInvariant())}");

        if (hasProviders)
        {
            parameters.Add(
                $"with_watch_providers={string.Join('|', criteria.WatchProviderIds.OrderBy(id => id))}");
        }

        if (hasMonetization)
        {
            parameters.Add(
                $"with_watch_monetization_types={string.Join('|', criteria.WatchMonetizationTypes.Select(MapMonetizationType))}");
        }
    }

    internal static string MapMonetizationType(WatchMonetizationType monetizationType) =>
        monetizationType switch
        {
            WatchMonetizationType.Stream => "flatrate",
            WatchMonetizationType.Free => "free",
            WatchMonetizationType.Ads => "ads",
            WatchMonetizationType.Rent => "rent",
            WatchMonetizationType.Buy => "buy",
            _ => "flatrate"
        };

    private static void AppendMovieOnlyFilters(
        List<string> parameters,
        AdvancedDiscoverProviderCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Certification) &&
            !string.IsNullOrWhiteSpace(criteria.CertificationCountry))
        {
            parameters.Add(
                $"certification={Uri.EscapeDataString(criteria.Certification.Trim())}");
            parameters.Add(
                $"certification_country={Uri.EscapeDataString(criteria.CertificationCountry.Trim().ToUpperInvariant())}");
        }

        if (criteria.ReleaseTypes.Count > 0)
        {
            var releaseTypeIds = criteria.ReleaseTypes
                .Distinct()
                .OrderBy(type => (int)type)
                .Select(type => ((int)type).ToString(System.Globalization.CultureInfo.InvariantCulture));

            parameters.Add($"with_release_type={string.Join('|', releaseTypeIds)}");
        }
    }

    private static void AppendKeywords(List<string> parameters, IReadOnlyList<int> keywordTmdbIds)
    {
        if (keywordTmdbIds.Count == 0)
        {
            return;
        }

        parameters.Add($"with_keywords={string.Join('|', keywordTmdbIds)}");
    }

    private static void AppendTvStatuses(List<string> parameters, IReadOnlyList<TvDiscoverStatus> tvStatuses)
    {
        if (tvStatuses.Count == 0)
        {
            return;
        }

        var statusIds = tvStatuses
            .Distinct()
            .OrderBy(status => (int)status)
            .Select(status => ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture));

        parameters.Add($"with_status={string.Join('|', statusIds)}");
    }

    private static void AppendMovieYearFilters(List<string> parameters, AdvancedDiscoverProviderCriteria criteria)
    {
        if (criteria.Year.HasValue)
        {
            parameters.Add($"primary_release_year={criteria.Year.Value}");
            return;
        }

        if (criteria.YearFrom.HasValue)
        {
            parameters.Add($"primary_release_date.gte={criteria.YearFrom.Value:0000}-01-01");
        }

        var upperBound = ResolveAdvancedMovieReleaseUpperBound(criteria);
        if (upperBound.HasValue)
        {
            parameters.Add($"primary_release_date.lte={upperBound.Value:yyyy-MM-dd}");
        }
    }

    private static void AppendTvYearFilters(List<string> parameters, AdvancedDiscoverProviderCriteria criteria)
    {
        if (criteria.Year.HasValue)
        {
            parameters.Add($"first_air_date_year={criteria.Year.Value}");
            return;
        }

        if (criteria.YearFrom.HasValue)
        {
            parameters.Add($"first_air_date.gte={criteria.YearFrom.Value:0000}-01-01");
        }

        var upperBound = ResolveAdvancedTvReleaseUpperBound(criteria);
        if (upperBound.HasValue)
        {
            parameters.Add($"first_air_date.lte={upperBound.Value:yyyy-MM-dd}");
        }
    }

    private static DateOnly? ResolveAdvancedMovieReleaseUpperBound(AdvancedDiscoverProviderCriteria criteria)
    {
        if (criteria.Year.HasValue)
        {
            return null;
        }

        DateOnly? userUpper = criteria.YearTo.HasValue
            ? new DateOnly(criteria.YearTo.Value, 12, 31)
            : null;

        DateOnly? futureCap = DiscoverCatalogSortPolicy.RequiresFutureReleaseCap(criteria.Sort)
            ? DiscoverCatalogSortPolicy.GetFutureReleaseCutoffDate(DateTime.UtcNow)
            : null;

        return DiscoverCatalogSortPolicy.ResolveReleaseUpperBound(userUpper, futureCap, null);
    }

    private static DateOnly? ResolveAdvancedTvReleaseUpperBound(AdvancedDiscoverProviderCriteria criteria)
    {
        if (criteria.Year.HasValue)
        {
            return null;
        }

        DateOnly? userUpper = criteria.YearTo.HasValue
            ? new DateOnly(criteria.YearTo.Value, 12, 31)
            : null;

        DateOnly? futureCap = DiscoverCatalogSortPolicy.RequiresFutureReleaseCap(criteria.Sort)
            ? DiscoverCatalogSortPolicy.GetFutureReleaseCutoffDate(DateTime.UtcNow)
            : null;

        return DiscoverCatalogSortPolicy.ResolveReleaseUpperBound(userUpper, futureCap, null);
    }

    private static string MapMovieSort(AdvancedDiscoverSort sort) =>
        sort switch
        {
            AdvancedDiscoverSort.RatingDesc => "vote_average.desc",
            AdvancedDiscoverSort.Newest => "primary_release_date.desc",
            AdvancedDiscoverSort.Oldest => "primary_release_date.asc",
            _ => "popularity.desc"
        };

    private static string MapTvSort(AdvancedDiscoverSort sort) =>
        sort switch
        {
            AdvancedDiscoverSort.RatingDesc => "vote_average.desc",
            AdvancedDiscoverSort.Newest => "first_air_date.desc",
            AdvancedDiscoverSort.Oldest => "first_air_date.asc",
            _ => "popularity.desc"
        };
}
