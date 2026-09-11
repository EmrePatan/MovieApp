namespace MovieApp.Application.Models.Recommendations;

public sealed record ScoredRecommendation(
    PersonalizedCandidateProfile Candidate,
    decimal Score,
    string? Reason);
