namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogTitleKeywordReadRepository
{
    Task<IReadOnlyList<string>> GetLocalizedKeywordNamesForMovieAsync(
        Guid movieId,
        string contentLocale,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLocalizedKeywordNamesForTvShowAsync(
        Guid tvShowId,
        string contentLocale,
        int maxCount,
        CancellationToken cancellationToken = default);
}
