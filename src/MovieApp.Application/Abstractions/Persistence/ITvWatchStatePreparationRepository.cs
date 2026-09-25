namespace MovieApp.Application.Abstractions.Persistence;

public interface ITvWatchStatePreparationRepository
{
    Task<TvWatchStatePreparation> PrepareAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
