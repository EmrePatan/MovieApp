using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IUserStatisticsRepository
{
    Task<UserStatisticsResult> GetStatisticsAsync(
        Guid userId,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default);
}
