namespace MovieApp.Application.Models.Reviews;

public sealed record ReviewTranslationResult(
    Guid ReviewId,
    ReviewTranslationOutcome Outcome,
    string? TranslatedText,
    string? DetectedSourceLanguage,
    string TargetLocale);
