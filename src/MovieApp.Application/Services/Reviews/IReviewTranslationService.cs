using MovieApp.Application.Models.Reviews;

namespace MovieApp.Application.Services.Reviews;

public interface IReviewTranslationService
{
    Task<ReviewTranslationResult> TranslateAsync(
        Guid reviewId,
        string targetContentLocale,
        CancellationToken cancellationToken = default);
}
