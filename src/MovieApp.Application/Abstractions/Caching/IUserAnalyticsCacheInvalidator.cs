namespace MovieApp.Application.Abstractions.Caching;

public interface IUserAnalyticsCacheInvalidator
{
    Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
