using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Images;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Localization;

public sealed class ContentLocalizedPosterSynchronizer(
    IContentLocalizedPosterRepository repository) : IContentLocalizedPosterSynchronizer
{
    public async Task SyncFromProviderPostersAsync(
        CatalogContentType contentType,
        Guid contentId,
        string? canonicalPosterPath,
        IReadOnlyList<ProviderImageResult>? providerPosters,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        if (providerPosters is null || providerPosters.Count == 0)
        {
            foreach (var languageKey in SupportedArtworkLanguageKeys.ForProviderEnrichment)
            {
                await repository.DeleteAsync(contentType, contentId, languageKey, cancellationToken);
            }

            return;
        }

        foreach (var languageKey in SupportedArtworkLanguageKeys.ForProviderEnrichment)
        {
            var selection = LocalizedPosterSelector.Select(providerPosters, languageKey, canonicalPosterPath);
            if (selection.ShouldPersist && !string.IsNullOrWhiteSpace(selection.PosterPath))
            {
                await repository.UpsertAsync(
                    contentType,
                    contentId,
                    languageKey,
                    selection.PosterPath,
                    utcNow,
                    cancellationToken);
            }
            else
            {
                await repository.DeleteAsync(contentType, contentId, languageKey, cancellationToken);
            }
        }
    }
}
