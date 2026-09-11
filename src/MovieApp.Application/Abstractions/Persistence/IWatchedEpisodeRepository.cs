using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IWatchedEpisodeRepository
{
    Task<WatchedEpisode?> GetByUserAndEpisodeAsync(
        Guid userId,
        Guid episodeId,
        CancellationToken cancellationToken = default);

    Task<(WatchedEpisode Entity, bool Created)> UpsertAsync(
        WatchedEpisode watchedEpisode,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid userId, Guid episodeId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WatchedEpisode> Items, int TotalCount)> GetUserWatchedEpisodesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountWatchedForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<int> CountWatchedForSeasonAsync(
        Guid userId,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(
        Guid EpisodeId,
        Guid TvShowId,
        Guid SeasonId,
        string TvShowTitle,
        int SeasonNumber,
        int EpisodeNumber,
        string? EpisodeTitle,
        DateTime WatchedAt)>> GetRecentForUserAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(
        Guid TvShowId,
        string Title,
        string? OriginalTitle,
        string? PosterUrl,
        string? BackdropUrl,
        DateOnly? FirstAirDate,
        decimal VoteAverage,
        int VoteCount,
        DateTime LastWatchedAt)>> GetContinueWatchingTvShowsAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default);
}
