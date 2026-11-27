using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Caching;

public sealed class TvShowDetailsCacheEntry
{
    public required TvShowDetailsResult Result { get; init; }

    /// <summary>
    /// The provider answered and the show has no seasons. Without this an empty season list
    /// may come from a failed provider call, so the entry is not trusted.
    /// </summary>
    public bool SeasonsConfirmedEmpty { get; init; }
}
