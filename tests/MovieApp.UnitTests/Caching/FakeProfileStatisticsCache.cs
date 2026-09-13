using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Identity;

namespace MovieApp.UnitTests.Caching;

public sealed class FakeProfileStatisticsCache : IProfileStatisticsCache
{
    public int InvalidateCount { get; private set; }

    public List<Guid> InvalidatedUserIds { get; } = [];

    public Task<UserStatisticsResult?> GetAsync(
        Guid userId,
        string? timeZoneId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<UserStatisticsResult?>(null);

    public Task SetAsync(
        Guid userId,
        string? timeZoneId,
        UserStatisticsResult statistics,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        InvalidateCount++;
        InvalidatedUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
