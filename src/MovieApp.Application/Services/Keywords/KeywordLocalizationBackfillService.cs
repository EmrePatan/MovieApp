using System.Diagnostics;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Keywords;

public sealed class KeywordLocalizationBackfillService(
    IKeywordLocalizationBackfillRepository backfillRepository,
    IKeywordBatchTranslationProvider translationProvider) : IKeywordLocalizationBackfillService
{
    public async Task<KeywordLocalizationDryRunPlan> PlanDryRunAsync(
        KeywordLocalizationBackfillRequest request,
        CancellationToken cancellationToken = default)
    {
        var locale = NormalizeTargetLocale(request.Locale);
        var maxItems = Math.Max(1, request.MaxItems);
        var counts = await backfillRepository.GetDryRunCountsAsync(locale, cancellationToken);
        var wouldTranslate = Math.Min(maxItems, counts.Missing + counts.StaleMachine);

        return new KeywordLocalizationDryRunPlan(
            locale,
            counts.Missing + counts.StaleMachine + counts.ProtectedCuratedOrReviewed + counts.Current,
            counts.Missing,
            counts.StaleMachine,
            counts.ProtectedCuratedOrReviewed,
            wouldTranslate);
    }

    public async Task<KeywordLocalizationBackfillResult> BackfillAsync(
        KeywordLocalizationBackfillRequest request,
        CancellationToken cancellationToken = default)
    {
        var locale = NormalizeTargetLocale(request.Locale);
        var batchSize = Math.Clamp(request.BatchSize, 1, 100);
        var maxItems = Math.Max(1, request.MaxItems);
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, request.DelayBetweenBatchesMs));
        var stopwatch = Stopwatch.StartNew();

        if (request.DryRun)
        {
            var plan = await PlanDryRunAsync(request, cancellationToken);
            stopwatch.Stop();
            return new KeywordLocalizationBackfillResult(
                locale,
                plan.WouldTranslate,
                0,
                0,
                0,
                plan.EligibleCandidates - plan.WouldTranslate - plan.ProtectedCuratedOrReviewed,
                plan.ProtectedCuratedOrReviewed,
                0,
                0,
                0,
                stopwatch.ElapsedMilliseconds,
                true);
        }

        var selected = 0;
        var translated = 0;
        var inserted = 0;
        var updated = 0;
        var skippedCurrent = 0;
        var skippedCurated = 0;
        var staleReviewRequired = 0;
        var failed = 0;
        var providerRequests = 0;
        Guid? cursor = request.StartAfterKeywordId;

        while (selected < maxItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var take = Math.Min(batchSize, maxItems - selected);
            var candidates = await backfillRepository.SelectCandidatesAsync(
                locale,
                take,
                cursor,
                cancellationToken);

            if (candidates.Count == 0)
            {
                break;
            }

            selected += candidates.Count;
            cursor = candidates[^1].KeywordId;

            var sourceTexts = candidates.Select(candidate => candidate.SourceText).ToList();
            IReadOnlyList<string> translations;
            try
            {
                translations = await translationProvider.TranslateAsync(
                    sourceTexts,
                    locale,
                    cancellationToken);
                providerRequests++;
            }
            catch (KeywordTranslationQuotaExceededException)
            {
                throw;
            }

            if (translations.Count != candidates.Count)
            {
                failed += candidates.Count;
                continue;
            }

            var upserts = new List<KeywordLocalizationMachineUpsert>(candidates.Count);
            var now = DateTime.UtcNow;

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var translation = translations[index];
                if (!KeywordLocalizationTranslationValidator.TryValidateTranslatedName(
                        translation,
                        out var normalizedName,
                        out _))
                {
                    failed++;
                    continue;
                }

                translated++;
                upserts.Add(new KeywordLocalizationMachineUpsert(
                    candidate.KeywordId,
                    locale,
                    translation.Trim(),
                    normalizedName,
                    candidate.SourceTextHash,
                    now));
            }

            if (upserts.Count > 0)
            {
                var upsertResult = await backfillRepository.UpsertMachineTranslationsAsync(
                    upserts,
                    cancellationToken);
                inserted += upsertResult.Inserted;
                updated += upsertResult.Updated;
                skippedCurated += upsertResult.SkippedProtected;
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        staleReviewRequired = await backfillRepository.CountStaleProtectedAsync(locale, cancellationToken);
        stopwatch.Stop();

        return new KeywordLocalizationBackfillResult(
            locale,
            selected,
            translated,
            inserted,
            updated,
            skippedCurrent,
            skippedCurated,
            staleReviewRequired,
            failed,
            providerRequests,
            stopwatch.ElapsedMilliseconds,
            false);
    }

    private static string NormalizeTargetLocale(string locale)
    {
        var normalized = SupportedContentLocales.Normalize(locale);
        if (string.Equals(normalized, SupportedContentLocales.EnglishUnitedStates, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("English (en-US) is not a keyword bulk translation target locale.", nameof(locale));
        }

        if (!SupportedContentLocales.KeywordBulkTranslationTargetLocales.Contains(
                normalized,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Locale '{locale}' is not supported for keyword bulk translation.", nameof(locale));
        }

        return normalized;
    }
}
