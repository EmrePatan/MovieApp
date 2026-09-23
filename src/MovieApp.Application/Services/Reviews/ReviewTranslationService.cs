using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Reviews;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Reviews;

public sealed class ReviewTranslationService(
    IReviewRepository reviewRepository,
    IReviewTranslationProvider translationProvider,
    ICacheService cacheService,
    ILogger<ReviewTranslationService> logger) : IReviewTranslationService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);

    public async Task<ReviewTranslationResult> TranslateAsync(
        Guid reviewId,
        string targetContentLocale,
        CancellationToken cancellationToken = default)
    {
        if (reviewId == Guid.Empty)
        {
            throw new ValidationException("Review id is required.");
        }

        var normalizedTarget = SupportedContentLocales.Normalize(targetContentLocale);
        if (!SupportedContentLocales.IsSupported(normalizedTarget))
        {
            throw new ValidationException("The requested target locale is not supported.");
        }

        ReviewTranslationLogMessages.LogTranslationRequested(logger, reviewId, normalizedTarget);

        var review = await reviewRepository.GetTranslationSourceByIdAsync(reviewId, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException("The requested review was not found.");
        }

        var contentValidation = ReviewContentValidator.Validate(review.Content);
        if (!contentValidation.IsValid)
        {
            throw new ValidationException(contentValidation.ErrorMessage!);
        }

        var cacheKey = ReviewTranslationCacheKeys.For(review.Id, review.UpdatedAt, normalizedTarget);
        var cached = await cacheService.GetAsync<ReviewTranslationCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            ReviewTranslationLogMessages.LogCacheHit(logger, reviewId, normalizedTarget);
            return ToResult(reviewId, cached);
        }

        ReviewTranslationLogMessages.LogCacheMiss(logger, reviewId, normalizedTarget);

        ReviewTranslationProviderResult providerResult;
        try
        {
            providerResult = await translationProvider.TranslateAsync(
                review.Content,
                normalizedTarget,
                cancellationToken);
        }
        catch (ReviewTranslationQuotaExceededException)
        {
            ReviewTranslationLogMessages.LogProviderQuotaExceeded(logger, reviewId, normalizedTarget);
            throw new ReviewTranslationUnavailableException();
        }
        catch (ReviewTranslationProviderException)
        {
            ReviewTranslationLogMessages.LogProviderUnavailable(logger, reviewId, normalizedTarget);
            throw new ReviewTranslationUnavailableException();
        }

        var outcome = providerResult.SourceMatchesTarget
            ? ReviewTranslationOutcome.SourceMatchesTarget
            : ReviewTranslationOutcome.Translated;

        var result = new ReviewTranslationResult(
            reviewId,
            outcome,
            providerResult.SourceMatchesTarget ? null : providerResult.TranslatedText,
            providerResult.DetectedSourceLanguage,
            normalizedTarget);

        await cacheService.SetAsync(
            cacheKey,
            new ReviewTranslationCacheEntry
            {
                Outcome = outcome,
                TranslatedText = result.TranslatedText,
                DetectedSourceLanguage = result.DetectedSourceLanguage,
                TargetLocale = normalizedTarget,
            },
            CacheTtl,
            cancellationToken);

        ReviewTranslationLogMessages.LogTranslationSucceeded(
            logger,
            reviewId,
            normalizedTarget,
            outcome == ReviewTranslationOutcome.SourceMatchesTarget
                ? "SourceMatchesTarget"
                : "Translated");

        return result;
    }

    private static ReviewTranslationResult ToResult(Guid reviewId, ReviewTranslationCacheEntry cached) =>
        new(
            reviewId,
            cached.Outcome,
            cached.TranslatedText,
            cached.DetectedSourceLanguage,
            cached.TargetLocale);
}
