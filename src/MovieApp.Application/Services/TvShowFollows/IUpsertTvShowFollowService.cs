using MovieApp.Application.Models.TvShowFollows;

namespace MovieApp.Application.Services.TvShowFollows;

public interface IUpsertTvShowFollowService
{
    Task<(TvShowFollowMutationResult Mutation, TvShowFollowStatusResult Status)> UpsertAsync(
        Guid tvShowId,
        TvShowFollowPreferencesUpdate preferences,
        CancellationToken cancellationToken = default);
}
