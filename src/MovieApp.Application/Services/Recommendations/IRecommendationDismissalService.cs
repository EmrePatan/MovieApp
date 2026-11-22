namespace MovieApp.Application.Services.Recommendations;

public interface IRecommendationDismissalService
{
    /// <summary>
    /// Records a "not interested" action for the current user. Repeating the action is a no-op.
    /// </summary>
    Task DismissMovieAsync(Guid movieId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a "not interested" action for the current user. Repeating the action is a no-op.
    /// </summary>
    Task DismissTvShowAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
