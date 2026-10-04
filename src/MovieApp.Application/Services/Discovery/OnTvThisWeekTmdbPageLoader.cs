using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.Discovery;

/// <summary>
/// Loads TMDB <c>tv/on_the_air</c> pages for On TV This Week. May fetch a second consecutive TMDB
/// page when filtered results cannot fill <paramref name="pageSize"/> (max 2 TMDB pages per request).
/// </summary>
public static class OnTvThisWeekTmdbPageLoader
{
    public const int MaxTmdbPagesPerRequest = 2;

    public static async Task<OnTvThisWeekTmdbLoadResult> LoadOrderedSummariesAsync(
        IOnTvThisWeekCatalog catalog,
        int requestPage,
        int pageSize,
        OnTvThisWeekPresentationIntent presentationIntent,
        CancellationToken cancellationToken)
    {
        var firstPage = await catalog.GetOnTheAirTvShowsAsync(requestPage, cancellationToken);
        var merged = firstPage.Results.ToList();
        var orderedSummaries = SelectEligibleInSourceOrder(merged, presentationIntent);
        var pagesFetched = 1;

        if (orderedSummaries.Count >= pageSize || firstPage.Page >= firstPage.TotalPages)
        {
            return new OnTvThisWeekTmdbLoadResult(orderedSummaries, firstPage, pagesFetched);
        }

        var nextTmdbPage = requestPage + 1;
        var secondPage = await catalog.GetOnTheAirTvShowsAsync(nextTmdbPage, cancellationToken);
        MergeUniqueByTmdbId(merged, secondPage.Results);
        orderedSummaries = SelectEligibleInSourceOrder(merged, presentationIntent);
        pagesFetched = 2;

        return new OnTvThisWeekTmdbLoadResult(orderedSummaries, firstPage, pagesFetched);
    }

    internal static List<TvShowProviderSummary> SelectEligibleInSourceOrder(
        IReadOnlyList<TvShowProviderSummary> summariesInProviderOrder,
        OnTvThisWeekPresentationIntent presentationIntent)
    {
        var qualified = summariesInProviderOrder
            .Where(summary => OnTvThisWeekQualityFilter.Include(summary.VoteCount, summary.PosterPath))
            .ToList();

        return OnTvThisWeekContentSelector
            .OrderForPresentation(qualified, presentationIntent)
            .ToList();
    }

    internal static void MergeUniqueByTmdbId(
        List<TvShowProviderSummary> merged,
        IReadOnlyList<TvShowProviderSummary> additional)
    {
        var seenTmdbIds = merged
            .Where(summary => summary.TmdbId is not null)
            .Select(summary => summary.TmdbId!.Value)
            .ToHashSet();

        foreach (var summary in additional)
        {
            if (summary.TmdbId is int tmdbId)
            {
                if (!seenTmdbIds.Add(tmdbId))
                {
                    continue;
                }
            }

            merged.Add(summary);
        }
    }
}

public sealed record OnTvThisWeekTmdbLoadResult(
    IReadOnlyList<TvShowProviderSummary> OrderedSummaries,
    TvShowProviderSearchResult MetadataSource,
    int TmdbPagesFetched);
