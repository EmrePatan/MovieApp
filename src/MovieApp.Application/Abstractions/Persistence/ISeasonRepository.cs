using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ISeasonRepository
{
    Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default);

    Task<Season> UpsertFromProviderAsync(
        Guid tvShowId,
        SeasonProviderDetails details,
        CancellationToken cancellationToken = default);

    Task<Season> UpsertSummaryFromProviderAsync(
        Guid tvShowId,
        SeasonProviderSummary summary,
        CancellationToken cancellationToken = default);
}
