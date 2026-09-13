using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Abstractions.Caching;

public interface IProfileStatisticsCache
{
    Task<UserStatisticsResult?> GetAsync(
        Guid userId,
        string? timeZoneId,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        Guid userId,
        string? timeZoneId,
        UserStatisticsResult statistics,
        CancellationToken cancellationToken = default);

    Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
