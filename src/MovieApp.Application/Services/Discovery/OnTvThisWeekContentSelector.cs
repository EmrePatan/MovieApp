using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.Discovery;

/// <summary>
/// Applies On TV This Week genre policy to quality-filtered TMDB <c>tv/on_the_air</c> candidates.
/// Home and Discover intents share the same exclusions today but remain separate entry points.
/// </summary>
public static class OnTvThisWeekContentSelector
{
    public const int TalkTmdbGenreId = 10767;
    public const int NewsTmdbGenreId = 10763;
    public const int RealityTmdbGenreId = 10764;

    public static IReadOnlyList<TvShowProviderSummary> OrderForPresentation(
        IReadOnlyList<TvShowProviderSummary> qualifiedInProviderOrder,
        OnTvThisWeekPresentationIntent presentationIntent)
    {
        return presentationIntent switch
        {
            OnTvThisWeekPresentationIntent.HomeRail => FilterEligibleInSourceOrder(qualifiedInProviderOrder),
            OnTvThisWeekPresentationIntent.DiscoverBrowse => FilterEligibleInSourceOrder(qualifiedInProviderOrder),
            _ => FilterEligibleInSourceOrder(qualifiedInProviderOrder),
        };
    }

    private static List<TvShowProviderSummary> FilterEligibleInSourceOrder(
        IReadOnlyList<TvShowProviderSummary> qualifiedInProviderOrder)
    {
        var result = new List<TvShowProviderSummary>(qualifiedInProviderOrder.Count);
        foreach (var summary in qualifiedInProviderOrder)
        {
            if (IsExcludedNonScriptedGenre(summary.GenreTmdbIds))
            {
                continue;
            }

            result.Add(summary);
        }

        return result;
    }

    internal static bool IsExcludedNonScriptedGenre(IReadOnlyList<int>? genreTmdbIds) =>
        ContainsGenre(genreTmdbIds, TalkTmdbGenreId)
        || ContainsGenre(genreTmdbIds, NewsTmdbGenreId)
        || ContainsGenre(genreTmdbIds, RealityTmdbGenreId);

    private static bool ContainsGenre(IReadOnlyList<int>? genreTmdbIds, int genreId) =>
        genreTmdbIds is not null && genreTmdbIds.Contains(genreId);
}
