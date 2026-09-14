using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShowFollows;

public interface ITvShowFollowBaselineService
{
    Task EstablishAsync(CatalogFollow follow, CancellationToken cancellationToken = default);
}
