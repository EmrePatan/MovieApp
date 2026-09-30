using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class KeywordLocalizationBackfillRepository(ApplicationDbContext dbContext)
    : IKeywordLocalizationBackfillRepository
{
    private const int CandidateScanChunkSize = 200;

    public async Task<IReadOnlyList<KeywordLocalizationBackfillCandidate>> SelectCandidatesAsync(
        string locale,
        int take,
        Guid? startAfterKeywordId,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        var results = new List<KeywordLocalizationBackfillCandidate>(take);
        var cursor = startAfterKeywordId;

        while (results.Count < take)
        {
            var chunk = await LoadKeywordChunkAsync(locale, cursor, CandidateScanChunkSize, cancellationToken);
            if (chunk.Count == 0)
            {
                break;
            }

            foreach (var row in chunk)
            {
                cursor = row.KeywordId;
                var classification = Classify(row);
                if (classification is null)
                {
                    continue;
                }

                results.Add(classification);
                if (results.Count >= take)
                {
                    break;
                }
            }

            if (chunk.Count < CandidateScanChunkSize)
            {
                break;
            }
        }

        return results;
    }

    public async Task<KeywordLocalizationMachineUpsertResult> UpsertMachineTranslationsAsync(
        IReadOnlyList<KeywordLocalizationMachineUpsert> upserts,
        CancellationToken cancellationToken = default)
    {
        if (upserts.Count == 0)
        {
            return new KeywordLocalizationMachineUpsertResult(0, 0, 0);
        }

        return await dbContext.Database.ExecuteInRetriableTransactionAsync(
            innerCancellationToken => KeywordLocalizationMachineUpsertSql.ExecuteBatchAsync(
                dbContext,
                upserts,
                innerCancellationToken),
            cancellationToken);
    }

    public async Task UpsertCuratedTranslationAsync(
        KeywordLocalizationCuratedUpsert upsert,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.KeywordLocalizations
            .FirstOrDefaultAsync(
                localization =>
                    localization.KeywordId == upsert.KeywordId &&
                    localization.Locale == upsert.Locale,
                cancellationToken);

        if (existing is null)
        {
            dbContext.KeywordLocalizations.Add(new KeywordLocalization
            {
                KeywordId = upsert.KeywordId,
                Locale = upsert.Locale,
                Name = upsert.Name,
                NormalizedName = upsert.NormalizedName,
                TranslationSource = KeywordTranslationSource.Curated,
                ReviewStatus = KeywordTranslationReviewStatus.Reviewed,
                SourceTextHash = upsert.SourceTextHash,
                CreatedAtUtc = upsert.UtcNow,
                UpdatedAtUtc = upsert.UtcNow,
            });
        }
        else
        {
            existing.Name = upsert.Name;
            existing.NormalizedName = upsert.NormalizedName;
            existing.TranslationSource = KeywordTranslationSource.Curated;
            existing.ReviewStatus = KeywordTranslationReviewStatus.Reviewed;
            existing.SourceTextHash = upsert.SourceTextHash;
            existing.UpdatedAtUtc = upsert.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<KeywordLocalizationDryRunCounts> GetDryRunCountsAsync(
        string locale,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Keywords
            .AsNoTracking()
            .Select(keyword => new
            {
                keyword.Id,
                keyword.Name,
                keyword.CanonicalName,
                Localization = keyword.Localizations
                    .Where(localization => localization.Locale == locale)
                    .Select(localization => new
                    {
                        localization.TranslationSource,
                        localization.ReviewStatus,
                        localization.SourceTextHash,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var missing = 0;
        var staleMachine = 0;
        var current = 0;
        var protectedCuratedOrReviewed = 0;

        foreach (var row in rows)
        {
            var sourceText = KeywordLocalizationSourceText.ResolveSourceText(new Keyword
            {
                Name = row.Name,
                CanonicalName = row.CanonicalName,
            });
            var hash = KeywordLocalizationSourceText.ComputeSourceTextHash(
                KeywordLocalizationSourceText.NormalizeSourceTextForHash(sourceText));

            if (row.Localization is null)
            {
                missing++;
                continue;
            }

            if (row.Localization.TranslationSource == KeywordTranslationSource.Curated ||
                row.Localization.ReviewStatus == KeywordTranslationReviewStatus.Reviewed)
            {
                protectedCuratedOrReviewed++;
                continue;
            }

            if (string.Equals(row.Localization.SourceTextHash, hash, StringComparison.Ordinal))
            {
                current++;
            }
            else
            {
                staleMachine++;
            }
        }

        return new KeywordLocalizationDryRunCounts(
            missing,
            staleMachine,
            current,
            protectedCuratedOrReviewed);
    }

    public async Task<int> CountStaleProtectedAsync(
        string locale,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from localization in dbContext.KeywordLocalizations.AsNoTracking()
            join keyword in dbContext.Keywords.AsNoTracking()
                on localization.KeywordId equals keyword.Id
            where localization.Locale == locale &&
                  (localization.TranslationSource == KeywordTranslationSource.Curated ||
                   localization.ReviewStatus == KeywordTranslationReviewStatus.Reviewed)
            select new
            {
                localization.SourceTextHash,
                keyword.Name,
                keyword.CanonicalName,
            })
            .ToListAsync(cancellationToken);

        var stale = 0;
        foreach (var row in rows)
        {
            var sourceText = KeywordLocalizationSourceText.ResolveSourceText(new Keyword
            {
                Name = row.Name,
                CanonicalName = row.CanonicalName,
            });
            var hash = KeywordLocalizationSourceText.ComputeSourceTextHash(
                KeywordLocalizationSourceText.NormalizeSourceTextForHash(sourceText));
            if (!string.Equals(row.SourceTextHash, hash, StringComparison.Ordinal))
            {
                stale++;
            }
        }

        return stale;
    }

    private async Task<IReadOnlyList<KeywordLocalizationScanRow>> LoadKeywordChunkAsync(
        string locale,
        Guid? startAfterKeywordId,
        int take,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Keywords.AsNoTracking().AsQueryable();
        if (startAfterKeywordId is not null)
        {
            query = query.Where(keyword => keyword.Id.CompareTo(startAfterKeywordId.Value) > 0);
        }

        return await query
            .OrderBy(keyword => keyword.Id)
            .Take(take)
            .Select(keyword => new KeywordLocalizationScanRow(
                keyword.Id,
                keyword.Name,
                keyword.CanonicalName,
                keyword.Localizations
                    .Where(localization => localization.Locale == locale)
                    .Select(localization => new KeywordLocalizationScanLocalization(
                        localization.TranslationSource,
                        localization.ReviewStatus,
                        localization.SourceTextHash))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    private static KeywordLocalizationBackfillCandidate? Classify(KeywordLocalizationScanRow row)
    {
        var keyword = new Keyword
        {
            Id = row.KeywordId,
            Name = row.Name,
            CanonicalName = row.CanonicalName,
        };
        var sourceText = KeywordLocalizationSourceText.ResolveSourceText(keyword);
        var sourceHash = KeywordLocalizationSourceText.ComputeSourceTextHash(
            KeywordLocalizationSourceText.NormalizeSourceTextForHash(sourceText));

        if (row.Localization is null)
        {
            return new KeywordLocalizationBackfillCandidate(
                row.KeywordId,
                sourceText,
                sourceHash,
                KeywordLocalizationBackfillCandidateAction.Insert);
        }

        if (row.Localization.TranslationSource == KeywordTranslationSource.Curated ||
            row.Localization.ReviewStatus == KeywordTranslationReviewStatus.Reviewed)
        {
            return null;
        }

        if (string.Equals(row.Localization.SourceTextHash, sourceHash, StringComparison.Ordinal))
        {
            return null;
        }

        return new KeywordLocalizationBackfillCandidate(
            row.KeywordId,
            sourceText,
            sourceHash,
            KeywordLocalizationBackfillCandidateAction.UpdateStaleMachine);
    }

    private sealed record KeywordLocalizationScanRow(
        Guid KeywordId,
        string Name,
        string? CanonicalName,
        KeywordLocalizationScanLocalization? Localization);

    private sealed record KeywordLocalizationScanLocalization(
        KeywordTranslationSource TranslationSource,
        KeywordTranslationReviewStatus ReviewStatus,
        string SourceTextHash);
}
