using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShowFollows;

public interface ITvShowFollowBaselineService
{
    Task EstablishAsync(TvShowFollow follow, CancellationToken cancellationToken = default);
}
