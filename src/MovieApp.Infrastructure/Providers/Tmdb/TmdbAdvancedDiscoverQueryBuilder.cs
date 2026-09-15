using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;

namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbAdvancedDiscoverQueryBuilder
{
    public static string BuildMovieQuery(AdvancedDiscoverProviderCriteria criteria)
    {
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapMovieSort(criteria.Sort)}",
            "include_adult=false"
        };

        AppendSharedFilters(parameters, criteria);
        AppendMovieYearFilters(parameters, criteria);

        return string.Join('&', parameters);
    }

    public static string BuildTvQuery(AdvancedDiscoverProviderCriteria criteria)
    {
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapTvSort(criteria.Sort)}",
            "include_adult=false"
        };

        AppendSharedFilters(parameters, criteria);
        AppendTvYearFilters(parameters, criteria);

        return string.Join('&', parameters);
    }

    private static void AppendSharedFilters(List<string> parameters, AdvancedDiscoverProviderCriteria criteria)
    {
        if (criteria.GenreTmdbIds.Count > 0)
        {
            parameters.Add($"with_genres={string.Join(',', criteria.GenreTmdbIds)}");
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

        if (criteria.MinVoteCount.HasValue)
        {
            parameters.Add($"vote_count.gte={criteria.MinVoteCount.Value}");
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

        if (criteria.YearTo.HasValue)
        {
            parameters.Add($"primary_release_date.lte={criteria.YearTo.Value:0000}-12-31");
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

        if (criteria.YearTo.HasValue)
        {
            parameters.Add($"first_air_date.lte={criteria.YearTo.Value:0000}-12-31");
        }
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
