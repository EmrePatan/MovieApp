namespace MovieApp.Contracts.Reviews;

public sealed record ReviewTranslationResponse(
    Guid ReviewId,
    string Outcome,
    string? TranslatedText,
    string? DetectedSourceLanguage,
    string TargetLocale);
