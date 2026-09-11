using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IUserStatisticsRepository
{
    Task<UserStatisticsResult> GetStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);
}
