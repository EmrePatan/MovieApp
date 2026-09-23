using MovieApp.Application.Models.Reviews;

namespace MovieApp.Application.Caching;

public sealed class ReviewTranslationCacheEntry
{
    public ReviewTranslationOutcome Outcome { get; init; }

    public string? TranslatedText { get; init; }

    public string? DetectedSourceLanguage { get; init; }

    public string TargetLocale { get; init; } = string.Empty;
}
