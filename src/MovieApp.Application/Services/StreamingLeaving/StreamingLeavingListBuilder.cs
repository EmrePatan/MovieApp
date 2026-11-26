using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.StreamingLeaving;

/// <summary>Turns the stored rows of a region into one card per title, then filters, sorts and pages them.</summary>
public static class StreamingLeavingListBuilder
{
    private sealed record Card(StreamingLeavingRow Title, StreamingLeavingResultItem Result);

    public static StreamingLeavingResult Build(
        IReadOnlyList<StreamingLeavingRow> rows,
        StreamingLeavingCriteria criteria,
        int minimumItems)
    {
        var titles = rows.GroupBy(row => (row.MediaType, row.ContentId)).ToList();

        // The minimum applies to the region list. A filtered list may be shorter.
        if (titles.Count < minimumItems)
        {
            return StreamingLeavingResult.Empty(criteria.Page, criteria.PageSize);
        }

        var cards = new List<Card>();
        foreach (var title in titles)
        {
            var platforms = title.OrderBy(row => row.LeavesAtUtc).ThenBy(row => row.ServiceName).ToList();
            var first = platforms[0];
            var leavesAt = first.LeavesAtUtc;

            if (criteria.WatchProviderIds.Count > 0)
            {
                var mine = platforms.Find(
                    row => StreamingPlatformMap.Matches(row.ServiceId, criteria.WatchProviderIds));
                if (mine is null)
                {
                    continue;
                }

                leavesAt = mine.LeavesAtUtc;
            }

            if (!MatchesFilters(first, criteria))
            {
                continue;
            }

            cards.Add(new Card(first, new StreamingLeavingResultItem(
                ToSearchItem(first),
                leavesAt,
                platforms
                    .Select(row => new StreamingLeavingPlatform(
                        row.ServiceId, row.ServiceName, row.LeavesAtUtc, row.WatchLink))
                    .ToList())));
        }

        var items = Sort(cards, criteria.Sort)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(card => card.Result)
            .ToList();

        return new StreamingLeavingResult(items, criteria.Page, criteria.PageSize, cards.Count);
    }

    private static bool MatchesFilters(StreamingLeavingRow title, StreamingLeavingCriteria criteria)
    {
        if ((criteria.MediaType == SearchContentType.Movie && title.MediaType != CatalogContentType.Movie) ||
            (criteria.MediaType == SearchContentType.Tv && title.MediaType != CatalogContentType.Tv))
        {
            return false;
        }

        if (criteria.GenreIds.Count > 0)
        {
            var matches = criteria.MatchAllGenres
                ? criteria.GenreIds.All(title.GenreIds.Contains)
                : criteria.GenreIds.Any(title.GenreIds.Contains);
            if (!matches)
            {
                return false;
            }
        }

        // A title without the value never passes a filter on it.
        var year = title.ReleaseDate?.Year;
        if ((criteria.YearFrom is { } yearFrom && !(year >= yearFrom)) ||
            (criteria.YearTo is { } yearTo && !(year <= yearTo)))
        {
            return false;
        }

        if ((criteria.MinRating is { } minRating && title.VoteAverage < minRating) ||
            (criteria.MaxRating is { } maxRating && title.VoteAverage > maxRating) ||
            (criteria.MinVoteCount is { } minVotes && title.VoteCount < minVotes))
        {
            return false;
        }

        // TV shows have no stored runtime, so a runtime filter leaves them out.
        if ((criteria.MinRuntimeMinutes is { } minRuntime && !(title.RuntimeMinutes >= minRuntime)) ||
            (criteria.MaxRuntimeMinutes is { } maxRuntime && !(title.RuntimeMinutes <= maxRuntime)))
        {
            return false;
        }

        return Same(criteria.OriginalLanguage, title.OriginalLanguage) &&
            Same(criteria.OriginCountry, title.OriginCountryCode);

        static bool Same(string? wanted, string? actual) =>
            string.IsNullOrWhiteSpace(wanted) ||
            string.Equals(wanted.Trim(), actual, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Card> Sort(IReadOnlyList<Card> cards, StreamingLeavingSort sort)
    {
        var ordered = sort switch
        {
            StreamingLeavingSort.RatingDesc => cards.OrderByDescending(card => card.Title.VoteAverage),
            StreamingLeavingSort.RatingAsc => cards.OrderBy(card => card.Title.VoteAverage),
            StreamingLeavingSort.ReleaseDesc => cards.OrderByDescending(card => card.Title.ReleaseDate),
            StreamingLeavingSort.ReleaseAsc =>
                cards.OrderBy(card => card.Title.ReleaseDate ?? DateOnly.MaxValue),
            StreamingLeavingSort.TitleAsc =>
                cards.OrderBy(card => card.Title.Title, StringComparer.OrdinalIgnoreCase),
            StreamingLeavingSort.TitleDesc =>
                cards.OrderByDescending(card => card.Title.Title, StringComparer.OrdinalIgnoreCase),
            _ => cards.OrderBy(card => card.Result.LeavesAtUtc)
        };

        // Many titles share a date: show the better known first and keep paging stable.
        return ordered
            .ThenByDescending(card => card.Title.VoteCount)
            .ThenBy(card => card.Title.ContentId);
    }

    private static SearchItem ToSearchItem(StreamingLeavingRow row) =>
        new(
            row.ContentId,
            row.MediaType == CatalogContentType.Tv ? "tv" : "movie",
            row.Title,
            row.OriginalTitle,
            row.Overview,
            row.PosterPath,
            row.BackdropPath,
            row.ReleaseDate,
            row.VoteAverage,
            row.VoteCount,
            row.ReleaseDate?.Year,
            row.TmdbId);
}
