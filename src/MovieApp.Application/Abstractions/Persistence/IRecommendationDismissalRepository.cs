using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IRecommendationDismissalRepository
{
    Task<bool> ExistsForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the dismissal. Returns false when the user already dismissed the same title.
    /// </summary>
    Task<bool> TryAddAsync(RecommendationDismissal dismissal, CancellationToken cancellationToken = default);
}
