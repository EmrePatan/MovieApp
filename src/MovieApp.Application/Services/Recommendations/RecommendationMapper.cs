using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.Application.Services.Recommendations;

internal static class RecommendationMapper
{
    internal static RecommendationItem ToRecommendationItem(
        SimilarityCandidateProfile candidate,
        decimal score,
        string? reason) =>
        new(
            candidate.Id,
            candidate.Type,
            candidate.Title,
            candidate.OriginalTitle,
            candidate.Overview,
            candidate.PosterUrl,
            candidate.BackdropUrl,
            candidate.ReleaseDate,
            candidate.VoteAverage,
            candidate.VoteCount,
            candidate.Year,
            score,
            reason);

    internal static RecommendationItem ToRecommendationItem(ScoredRecommendation recommendation) =>
        new(
            recommendation.Candidate.Id,
            recommendation.Candidate.Type,
            recommendation.Candidate.Title,
            recommendation.Candidate.OriginalTitle,
            recommendation.Candidate.Overview,
            recommendation.Candidate.PosterUrl,
            recommendation.Candidate.BackdropUrl,
            recommendation.Candidate.ReleaseDate,
            recommendation.Candidate.VoteAverage,
            recommendation.Candidate.VoteCount,
            recommendation.Candidate.Year,
            recommendation.Score,
            recommendation.Reason);

    internal static RecommendationItem ToColdStartItem(
        Models.Search.SearchItem item,
        string reason) =>
        new(
            item.Id,
            item.Type,
            item.Title,
            item.OriginalTitle,
            item.Overview,
            item.PosterUrl,
            item.BackdropUrl,
            item.ReleaseDate,
            item.VoteAverage,
            item.VoteCount,
            item.Year,
            NormalizeColdStartScore(item.VoteAverage, item.VoteCount),
            reason);

    private static decimal NormalizeColdStartScore(decimal voteAverage, int voteCount)
    {
        var ratingComponent = voteAverage / 10m;
        var popularityComponent = Math.Min(1m, voteCount / 1000m);
        return Math.Round((ratingComponent * 0.7m) + (popularityComponent * 0.3m), 4, MidpointRounding.AwayFromZero);
    }
}
