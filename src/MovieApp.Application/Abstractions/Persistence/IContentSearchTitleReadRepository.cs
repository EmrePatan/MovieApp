using MovieApp.Application.Models.Search;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IContentSearchTitleReadRepository
{
    /// <summary>
    /// Resolves the best localized display title per content item for the requested locale (one query per content type batch).
    /// </summary>
    Task<IReadOnlyDictionary<CatalogContentKey, string>> GetLocaleDisplayTitlesAsync(
        CatalogContentType contentType,
        IReadOnlyList<Guid> contentIds,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
