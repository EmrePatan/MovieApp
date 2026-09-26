using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public readonly record struct ContentLocalizedPosterKey(CatalogContentType ContentType, Guid ContentId);

public interface IContentLocalizedPosterRepository
{
    Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> GetPosterPathsAsync(
        IReadOnlyList<ContentLocalizedPosterKey> keys,
        string languageKey,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        CatalogContentType contentType,
        Guid contentId,
        string languageKey,
        string posterPath,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        CatalogContentType contentType,
        Guid contentId,
        string languageKey,
        CancellationToken cancellationToken = default);
}
