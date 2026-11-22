using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Reviews;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed partial class SocialCommentTranslationService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IReviewTranslationProvider translationProvider,
    ICacheService cacheService,
    ILogger<SocialCommentTranslationService> logger) : ISocialCommentTranslationService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);

    [GeneratedRegex(@"\[gif\].*?\[/gif\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex GifTokenPattern();

    public async Task<ReviewTranslationResult> TranslateAsync(
        Guid commentId,
        string targetContentLocale,
        CancellationToken cancellationToken = default)
    {
        if (commentId == Guid.Empty)
        {
            throw new ValidationException("Comment id is required.");
        }

        var normalizedTarget = SupportedContentLocales.Normalize(targetContentLocale);
        if (!SupportedContentLocales.IsSupported(normalizedTarget))
        {
            throw new ValidationException("The requested target locale is not supported.");
        }

        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid viewerId || viewerId == Guid.Empty)
        {
            throw new AuthenticationException("Authentication is required.");
        }

        var comment = await dbContext.SocialComments.AsNoTracking()
            .Where(item => item.Id == commentId && !item.IsDeleted)
            .Select(item => new { item.Id, item.ActivityId, item.Content, item.UpdatedAt, item.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (comment is null)
        {
            throw new NotFoundException("Comment not found.");
        }

        await EnsureActivityVisibleAsync(comment.ActivityId, viewerId, cancellationToken);

        var sourceText = StripGifTokens(comment.Content).Trim();
        if (string.IsNullOrEmpty(sourceText))
        {
            throw new ValidationException("Nothing to translate in this comment.");
        }

        var versionStamp = comment.UpdatedAt ?? comment.CreatedAt;
        var cacheKey = $"social-comment-translation:{comment.Id:N}:{versionStamp.Ticks}:{normalizedTarget}";
        var cached = await cacheService.GetAsync<ReviewTranslationCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return new ReviewTranslationResult(
                comment.Id,
                cached.Outcome,
                cached.TranslatedText,
                cached.DetectedSourceLanguage,
                cached.TargetLocale);
        }

        ReviewTranslationProviderResult providerResult;
        try
        {
            providerResult = await translationProvider.TranslateAsync(
                sourceText,
                normalizedTarget,
                cancellationToken);
        }
        catch (ReviewTranslationQuotaExceededException)
        {
            SocialCommentTranslationLogMessages.LogQuotaExceeded(logger, commentId);
            throw new ReviewTranslationUnavailableException();
        }
        catch (ReviewTranslationProviderException)
        {
            SocialCommentTranslationLogMessages.LogProviderUnavailable(logger, commentId);
            throw new ReviewTranslationUnavailableException();
        }

        var outcome = providerResult.SourceMatchesTarget
            ? ReviewTranslationOutcome.SourceMatchesTarget
            : ReviewTranslationOutcome.Translated;

        var result = new ReviewTranslationResult(
            comment.Id,
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

        return result;
    }

    private static string StripGifTokens(string content) => GifTokenPattern().Replace(content, string.Empty).Trim();

    private async Task EnsureActivityVisibleAsync(Guid activityId, Guid viewerId, CancellationToken cancellationToken)
    {
        var activity = await (
            from item in dbContext.SocialActivities.AsNoTracking()
            join actor in dbContext.Users.AsNoTracking() on item.ActorId equals actor.Id
            where item.Id == activityId && !item.IsDeleted && actor.IsActive
            select new { item.ActorId, item.ActivityType }
        ).FirstOrDefaultAsync(cancellationToken);
        if (activity is null) throw new NotFoundException("Comment not found.");
        if (activity.ActorId == viewerId) return;

        var blocked = await dbContext.UserBlocks.AsNoTracking().AnyAsync(block =>
            (block.BlockerId == viewerId && block.BlockedUserId == activity.ActorId)
            || (block.BlockerId == activity.ActorId && block.BlockedUserId == viewerId), cancellationToken);
        if (blocked) throw new NotFoundException("Comment not found.");

        var privacy = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == activity.ActorId)
            .Select(settings => new { settings.IsPrivate, settings.ShowWatched, settings.ShowRatings, settings.ShowFavorites, settings.ShowReviews })
            .FirstOrDefaultAsync(cancellationToken);
        if (privacy?.IsPrivate == true && !await dbContext.UserFollows.AsNoTracking().AnyAsync(
            follow => follow.FollowerId == viewerId && follow.FollowedUserId == activity.ActorId
                && follow.Status == UserFollowStatus.Accepted, cancellationToken))
            throw new NotFoundException("Comment not found.");

        var visible = privacy is null || (activity.ActivityType switch
        {
            SocialActivityType.WatchedMovie or SocialActivityType.WatchedEpisode => privacy.ShowWatched,
            SocialActivityType.RatedMovie or SocialActivityType.RatedTvShow or SocialActivityType.RatedEpisode => privacy.ShowRatings,
            SocialActivityType.FavoritedMovie or SocialActivityType.FavoritedTvShow => privacy.ShowFavorites,
            _ => privacy.ShowReviews
        });
        if (!visible) throw new NotFoundException("Comment not found.");
    }
}