using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Contracts.ExternalRatings;

namespace MovieApp.Api.Mapping;

internal static class ExternalRatingsContractMapper
{
    public static ExternalRatingsResponse ToResponse(ExternalRatingsResult result) =>
        new(
            result.FetchedAtUtc,
            result.IsStale,
            result.Ratings
                .Select(rating => new ExternalRatingResponse(
                    rating.Source,
                    rating.Value,
                    rating.Scale,
                    rating.Votes))
                .ToList());
}
