using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbDiscoverQueryBuilder
{
    public const int TopRatedMinimumVoteCount = 50;

    public static string BuildMovieQuery(DiscoverProviderCriteria criteria)
    {
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapMovieSort(criteria)}"
        };

        AppendSharedFilters(parameters, criteria);

        if (criteria.Mode == DiscoverBrowseMode.TopRated)
        {
            parameters.Add($"vote_count.gte={TopRatedMinimumVoteCount}");
        }

        if (criteria.Mode == DiscoverBrowseMode.NewReleases)
        {
            parameters.Add($"primary_release_date.lte={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
        }

        if (criteria.Year.HasValue)
        {
            parameters.Add($"primary_release_year={criteria.Year.Value}");
        }

        return string.Join('&', parameters);
    }

    public static string BuildTvQuery(DiscoverProviderCriteria criteria)
    {
        var parameters = new List<string>
        {
            $"page={criteria.Page}",
            $"sort_by={MapTvSort(criteria)}"
        };

        AppendSharedFilters(parameters, criteria);

        if (criteria.Mode == DiscoverBrowseMode.TopRated)
        {
            parameters.Add($"vote_count.gte={TopRatedMinimumVoteCount}");
        }

        if (criteria.Mode == DiscoverBrowseMode.NewReleases)
        {
            parameters.Add($"first_air_date.lte={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
        }

        if (criteria.Year.HasValue)
        {
            parameters.Add($"first_air_date_year={criteria.Year.Value}");
        }

        return string.Join('&', parameters);
    }

    private static void AppendSharedFilters(List<string> parameters, DiscoverProviderCriteria criteria)
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

        if (!string.IsNullOrWhiteSpace(criteria.Language))
        {
            parameters.Add($"with_original_language={Uri.EscapeDataString(criteria.Language.Trim().ToLowerInvariant())}");
        }
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
