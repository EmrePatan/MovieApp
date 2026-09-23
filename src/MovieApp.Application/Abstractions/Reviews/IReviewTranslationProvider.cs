namespace MovieApp.Application.Abstractions.Reviews;

public interface IReviewTranslationProvider
{
    Task<ReviewTranslationProviderResult> TranslateAsync(
        string text,
        string targetContentLocale,
        CancellationToken cancellationToken = default);
}

public sealed record ReviewTranslationProviderResult(
    string? TranslatedText,
    string DetectedSourceLanguage,
    bool SourceMatchesTarget);
