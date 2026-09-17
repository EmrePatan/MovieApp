namespace MovieApp.Application.Models.AiRecommendations;

public sealed record AiProviderSuggestion(
    string Title,
    int Year,
    string MediaType,
    int? TmdbId,
    string Reason);

public sealed record AiProviderConstraintUpdates(
    IReadOnlyList<string> DesiredGenres,
    IReadOnlyList<string> ExcludedGenres,
    int? MaxRuntimeMinutes,
    int? MinYear,
    int? MaxYear,
    IReadOnlyList<string> MoodKeywords);

public sealed record AiProviderGenerationResult(
    IReadOnlyList<AiProviderSuggestion> Suggestions,
    AiProviderConstraintUpdates? ConstraintUpdates);

public sealed record AiProviderRequest(
    string UserMessage,
    AiTasteProfile TasteProfile,
    AiRecommendationSessionState Session,
    int SuggestionCount);

public sealed record ResolvedMovieIdentity(
    Guid MovieId,
    int? TmdbId,
    string Title,
    int? Year,
    int? RuntimeMinutes,
    string? OriginalTitle,
    string? Overview,
    string? PosterUrl,
    string? BackdropUrl,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    int VoteCount,
    IReadOnlyList<string> Genres);

public sealed record AiValidatedRecommendation(
    ResolvedMovieIdentity Movie,
    string Reason);

public sealed record AiValidationResult(
    IReadOnlyList<AiValidatedRecommendation> Recommendations,
    int GeminiSuggestionCount,
    int ValidatedCount,
    int RejectedCount,
    bool PartialResults);

public sealed record AiRecommendationServiceResult(
    Guid SessionId,
    bool IsAiGenerated,
    bool PartialResults,
    int RequestedCount,
    int ReturnedCount,
    int QuotaRemaining,
    IReadOnlyList<AiValidatedRecommendation> Recommendations,
    AiValidationResult ValidationSummary);

public sealed record AiQuotaReservation(string ReservationId);
