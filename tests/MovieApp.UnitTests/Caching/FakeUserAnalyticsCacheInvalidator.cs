using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.UnitTests.Caching;

public sealed class FakeUserAnalyticsCacheInvalidator : IUserAnalyticsCacheInvalidator
{
    public int InvalidateCount { get; private set; }

    public List<Guid> InvalidatedUserIds { get; } = [];

    public Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        InvalidateCount++;
        InvalidatedUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
