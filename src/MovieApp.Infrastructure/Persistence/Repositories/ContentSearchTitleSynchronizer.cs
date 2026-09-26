using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ContentSearchTitleSynchronizer(ApplicationDbContext dbContext) : IContentSearchTitleSynchronizer
{
    private static readonly ContentSearchTitleSource[] ProviderSources =
    [
        ContentSearchTitleSource.TmdbAlternative,
        ContentSearchTitleSource.TmdbTranslation,
    ];

    public async Task SyncCatalogTitlesAsync(
        CatalogContentType contentType,
        Guid contentId,
        string title,
        string? originalTitle,
        CancellationToken cancellationToken = default)
    {
        var desired = BuildCatalogRows(contentType, contentId, title, originalTitle, providerUpdatedAtUtc: null);
        await UpsertCatalogRowsAsync(contentType, contentId, desired, cancellationToken);
    }

    public async Task SyncFromProviderDetailAsync(
        CatalogContentType contentType,
        Guid contentId,
        string title,
        string? originalTitle,
        IReadOnlyList<ProviderSearchTitleEntry>? providerSearchTitles,
        DateTime? providerUpdatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var desired = BuildCatalogRows(contentType, contentId, title, originalTitle, providerUpdatedAtUtc);
        var providerNormalized = new HashSet<string>(StringComparer.Ordinal);

        if (providerSearchTitles is { Count: > 0 })
        {
            foreach (var entry in providerSearchTitles)
            {
                if (!TryAddDesiredRow(desired, entry, providerUpdatedAtUtc))
                {
                    continue;
                }

                if (ProviderSources.Contains(entry.Source))
                {
                    providerNormalized.Add(SearchTitleFolder.Fold(entry.Title));
                }
            }
        }

        await UpsertCatalogRowsAsync(contentType, contentId, desired, cancellationToken);
        await RemoveStaleProviderRowsAsync(contentType, contentId, providerNormalized, cancellationToken);
    }

    public async Task DeleteAllForContentAsync(
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.ContentSearchTitles
            .Where(row => row.ContentType == contentType && row.ContentId == contentId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task UpsertCatalogRowsAsync(
        CatalogContentType contentType,
        Guid contentId,
        Dictionary<string, DesiredRow> desired,
        CancellationToken cancellationToken)
    {
        if (desired.Count == 0)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var normalizedKeys = desired.Keys.ToList();
        var existingRows = await dbContext.ContentSearchTitles
            .Where(row => row.ContentType == contentType && row.ContentId == contentId)
            .Where(row => normalizedKeys.Contains(row.NormalizedTitle))
            .ToListAsync(cancellationToken);

        var existingByNormalized = existingRows.ToDictionary(row => row.NormalizedTitle, StringComparer.Ordinal);

        foreach (var (normalizedTitle, row) in desired)
        {
            if (existingByNormalized.TryGetValue(normalizedTitle, out var existing))
            {
                existing.Title = row.Title;
                existing.TitleKind = row.TitleKind;
                existing.Source = row.Source;
                existing.LanguageCode = row.LanguageCode;
                existing.CountryCode = row.CountryCode;
                existing.ProviderTitleType = row.ProviderTitleType;
                existing.ProviderUpdatedAtUtc = row.ProviderUpdatedAtUtc;
                existing.UpdatedAtUtc = utcNow;
                continue;
            }

            dbContext.ContentSearchTitles.Add(new ContentSearchTitle
            {
                Id = Guid.NewGuid(),
                ContentType = contentType,
                ContentId = contentId,
                Title = row.Title,
                NormalizedTitle = normalizedTitle,
                TitleKind = row.TitleKind,
                LanguageCode = row.LanguageCode,
                CountryCode = row.CountryCode,
                Source = row.Source,
                ProviderTitleType = row.ProviderTitleType,
                ProviderUpdatedAtUtc = row.ProviderUpdatedAtUtc,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveStaleProviderRowsAsync(
        CatalogContentType contentType,
        Guid contentId,
        HashSet<string> providerNormalized,
        CancellationToken cancellationToken)
    {
        await dbContext.ContentSearchTitles
            .Where(row =>
                row.ContentType == contentType
                && row.ContentId == contentId
                && ProviderSources.Contains(row.Source)
                && !providerNormalized.Contains(row.NormalizedTitle))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static Dictionary<string, DesiredRow> BuildCatalogRows(
        CatalogContentType contentType,
        Guid contentId,
        string title,
        string? originalTitle,
        DateTime? providerUpdatedAtUtc)
    {
        var desired = new Dictionary<string, DesiredRow>(StringComparer.Ordinal);
        TryAddCatalogRow(
            desired,
            contentType,
            contentId,
            title,
            ContentSearchTitleKind.Canonical,
            ContentSearchTitleSource.CatalogCanonical,
            providerUpdatedAtUtc);

        if (!string.IsNullOrWhiteSpace(originalTitle))
        {
            TryAddCatalogRow(
                desired,
                contentType,
                contentId,
                originalTitle,
                ContentSearchTitleKind.Original,
                ContentSearchTitleSource.CatalogOriginal,
                providerUpdatedAtUtc);
        }

        return desired;
    }

    private static void TryAddCatalogRow(
        Dictionary<string, DesiredRow> desired,
        CatalogContentType contentType,
        Guid contentId,
        string title,
        ContentSearchTitleKind titleKind,
        ContentSearchTitleSource source,
        DateTime? providerUpdatedAtUtc)
    {
        TryAddDesiredRow(
            desired,
            new ProviderSearchTitleEntry(title, titleKind, source, null, null, null),
            providerUpdatedAtUtc);
    }

    private static bool TryAddDesiredRow(
        Dictionary<string, DesiredRow> desired,
        ProviderSearchTitleEntry entry,
        DateTime? providerUpdatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(entry.Title))
        {
            return false;
        }

        var normalized = SearchTitleFolder.Fold(entry.Title);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        var displayTitle = QueryNormalizer.Normalize(entry.Title);
        if (desired.TryGetValue(normalized, out var existing))
        {
            if (ContentSearchTitleKindPrecedence.IsHigherPriority(entry.TitleKind, existing.TitleKind))
            {
                desired[normalized] = ToDesiredRow(entry, displayTitle, providerUpdatedAtUtc);
            }
            else if (entry.TitleKind == existing.TitleKind && displayTitle.Length > existing.Title.Length)
            {
                desired[normalized] = ToDesiredRow(entry, displayTitle, providerUpdatedAtUtc);
            }

            return true;
        }

        desired[normalized] = ToDesiredRow(entry, displayTitle, providerUpdatedAtUtc);
        return true;
    }

    private static DesiredRow ToDesiredRow(
        ProviderSearchTitleEntry entry,
        string displayTitle,
        DateTime? providerUpdatedAtUtc) =>
        new(
            displayTitle,
            entry.TitleKind,
            entry.Source,
            entry.LanguageCode,
            entry.CountryCode,
            entry.ProviderTitleType,
            providerUpdatedAtUtc);

    private sealed record DesiredRow(
        string Title,
        ContentSearchTitleKind TitleKind,
        ContentSearchTitleSource Source,
        string? LanguageCode,
        string? CountryCode,
        string? ProviderTitleType,
        DateTime? ProviderUpdatedAtUtc);
}
