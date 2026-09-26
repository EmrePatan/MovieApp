namespace MovieApp.Application.Abstractions.Persistence;

public interface IContentSearchTitleCatalogBackfillService
{
    /// <summary>
    /// Rebuilds canonical and original search-title rows from persisted catalog titles only.
    /// Does not call TMDB.
    /// </summary>
    Task BackfillCanonicalAndOriginalAsync(CancellationToken cancellationToken = default);
}
