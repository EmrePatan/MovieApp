namespace MovieApp.Application.Models.AiRecommendations;

public sealed class AiRecommendationSessionState
{
    public Guid SessionId { get; set; }

    public int TurnCount { get; set; }

    public List<string> DesiredGenres { get; set; } = [];

    public List<string> ExcludedGenres { get; set; } = [];

    public int? MaxRuntimeMinutes { get; set; }

    public int? MinYear { get; set; }

    public int? MaxYear { get; set; }

    public List<string> MoodKeywords { get; set; } = [];

    public HashSet<Guid> RecommendedMovieIds { get; set; } = [];

    public HashSet<int> RecommendedTmdbIds { get; set; } = [];

    public string? LastUserMessage { get; set; }
}
