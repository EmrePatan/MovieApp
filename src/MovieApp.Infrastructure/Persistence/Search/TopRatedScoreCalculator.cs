namespace MovieApp.Infrastructure.Persistence.Search;

internal static class TopRatedScoreCalculator
{
    public static decimal ComputeWeightedRating(
        decimal voteAverage,
        int voteCount,
        decimal catalogMeanVoteAverage,
        int minimumVoteConfidence)
    {
        var minimumVotes = (decimal)minimumVoteConfidence;
        var votes = voteCount;

        return votes / (votes + minimumVotes) * voteAverage
            + minimumVotes / (votes + minimumVotes) * catalogMeanVoteAverage;
    }
}
