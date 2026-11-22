using MovieApp.Application.Models.Reviews;

namespace MovieApp.Application.Abstractions.Social;

public interface ISocialCommentTranslationService
{
    Task<ReviewTranslationResult> TranslateAsync(
        Guid commentId,
        string targetContentLocale,
        CancellationToken cancellationToken = default);
}