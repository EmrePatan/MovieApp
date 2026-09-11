using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Recommendations;

public static class RecommendationReasonBuilder
{
    public static string BuildSimilarReason(SimilaritySourceProfile source, SimilarityCandidateProfile candidate)
    {
        var sharedGenre = source.GenreNames.Values
            .FirstOrDefault(genreName => candidate.GenreNames.Values.Contains(genreName));

        return sharedGenre is not null
            ? $"Because you liked {sharedGenre}"
            : $"Similar to {source.Title}";
    }

    public static string BuildColdStartPopularReason() => "Popular right now";

    public static string BuildColdStartTrendingReason() => "Trending";

    public static string BuildColdStartTopRatedReason() => "Top rated";
}
