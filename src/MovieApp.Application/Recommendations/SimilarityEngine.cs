using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Recommendations;

public static class SimilarityEngine
{
    public static decimal CalculateScore(
        SimilaritySourceProfile source,
        SimilarityCandidateProfile candidate,
        RecommendationOptions options)
    {
        var genreScore = CalculateOverlapScore(source.GenreIds, candidate.GenreIds);
        var castScore = CalculateOverlapScore(source.PersonIds, candidate.PersonIds);
        var ratingScore = CalculateRatingSimilarity(source.VoteAverage, candidate.VoteAverage);
        var yearScore = CalculateYearProximity(source.Year, candidate.Year);

        return RoundScore(
            (decimal)genreScore * (decimal)options.SimilarityGenreWeight +
            (decimal)castScore * (decimal)options.SimilarityCastWeight +
            (decimal)ratingScore * (decimal)options.SimilarityRatingWeight +
            (decimal)yearScore * (decimal)options.SimilarityYearWeight);
    }

    public static IReadOnlyList<(SimilarityCandidateProfile Candidate, decimal Score)> RankSimilarCandidates(
        SimilaritySourceProfile source,
        IReadOnlyList<SimilarityCandidateProfile> candidates,
        RecommendationOptions options)
    {
        return candidates
            .Where(candidate => candidate.Id != source.Id)
            .Select(candidate => (candidate, CalculateScore(source, candidate, options)))
            .OrderByDescending(item => item.Item2)
            .ThenByDescending(item => item.candidate.VoteCount)
            .ThenByDescending(item => item.candidate.VoteAverage)
            .ThenBy(item => item.candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static decimal CalculateOverlapScore(
        IReadOnlyList<Guid> sourceIds,
        IReadOnlyList<Guid> candidateIds)
    {
        if (sourceIds.Count == 0 || candidateIds.Count == 0)
        {
            return 0m;
        }

        var sourceSet = sourceIds.ToHashSet();
        var intersection = candidateIds.Count(candidateId => sourceSet.Contains(candidateId));
        if (intersection == 0)
        {
            return 0m;
        }

        var union = sourceSet.Count;
        foreach (var candidateId in candidateIds)
        {
            union += sourceSet.Contains(candidateId) ? 0 : 1;
        }

        return union == 0 ? 0m : (decimal)intersection / union;
    }

    internal static decimal CalculateRatingSimilarity(decimal sourceRating, decimal candidateRating)
    {
        var difference = Math.Abs((double)(sourceRating - candidateRating));
        return (decimal)Math.Max(0d, 1d - difference / 10d);
    }

    internal static decimal CalculateYearProximity(int? sourceYear, int? candidateYear)
    {
        if (sourceYear is null || candidateYear is null)
        {
            return 0.5m;
        }

        var difference = Math.Abs(sourceYear.Value - candidateYear.Value);
        return (decimal)Math.Max(0d, 1d - Math.Min(difference, 50) / 50d);
    }

    internal static decimal RoundScore(decimal score) =>
        Math.Round(score, 4, MidpointRounding.AwayFromZero);
}
