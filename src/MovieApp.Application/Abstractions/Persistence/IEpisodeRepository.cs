using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IEpisodeRepository
{
    Task<Episode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountByTvShowIdAsync(Guid tvShowId, CancellationToken cancellationToken = default);

    Task<int> CountByTvShowIdAndSeasonNumberAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default);

    Task<Episode?> GetFirstUnwatchedForTvShowAsync(
        Guid tvShowId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Episode?> GetFirstUnwatchedForSeasonAsync(
        Guid tvShowId,
        int seasonNumber,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Episode?> GetBySeasonIdAndEpisodeNumberAsync(
        Guid seasonId,
        int episodeNumber,
        CancellationToken cancellationToken = default);

    Task<Episode> UpsertFromProviderAsync(
        Guid seasonId,
        EpisodeProviderDetails details,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetEpisodeIdsBelongingToTvShowAsync(
        Guid tvShowId,
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowUpToEpisodeAsync(
        Guid tvShowId,
        Guid targetEpisodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetEpisodeIdsForSeasonAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);
}
