using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.UnitTests.Keywords;

internal sealed class NoOpCatalogTitleKeywordReadRepository : ICatalogTitleKeywordReadRepository
{
    public Task<IReadOnlyList<string>> GetLocalizedKeywordNamesForMovieAsync(
        Guid movieId,
        string contentLocale,
        int maxCount,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> GetLocalizedKeywordNamesForTvShowAsync(
        Guid tvShowId,
        string contentLocale,
        int maxCount,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
