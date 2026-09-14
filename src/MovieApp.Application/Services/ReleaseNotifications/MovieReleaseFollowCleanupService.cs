using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.Application.Services.ReleaseNotifications;

public sealed class MovieReleaseFollowCleanupService(ICatalogFollowRepository catalogFollowRepository)
    : IMovieReleaseFollowCleanupService
{
    public async Task CleanupAsync(
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var movieId in movieIds.Distinct())
        {
            await catalogFollowRepository.RemoveMovieFollowsByMovieIdAsync(movieId, cancellationToken);
        }
    }
}
