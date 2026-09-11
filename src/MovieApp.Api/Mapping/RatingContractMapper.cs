using MovieApp.Application.Models.Ratings;
using MovieApp.Contracts.Ratings;

namespace MovieApp.Api.Mapping;

public static class RatingContractMapper
{
    public static RatingResponse ToResponse(RatingResult result) =>
        new(
            result.Id,
            result.MovieId,
            result.TvShowId,
            result.Score,
            result.CreatedAt,
            result.UpdatedAt);

    public static RatingSummaryResponse ToSummaryResponse(RatingSummaryResult result) =>
        new(
            result.AverageScore,
            result.RatingCount,
            result.ScoreDistribution);
}
