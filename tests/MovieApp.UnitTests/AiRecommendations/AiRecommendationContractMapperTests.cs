using MovieApp.Api.Mapping;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiRecommendationContractMapperTests
{
    [Fact]
    public void ToResponseMapsResolvedMediaType()
    {
        var movieId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tvShowId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var result = new AiRecommendationServiceResult(
            Guid.NewGuid(),
            IsAiGenerated: true,
            PartialResults: false,
            RequestedCount: 5,
            ReturnedCount: 2,
            QuotaRemaining: 2,
            [
                new AiValidatedRecommendation(
                    new ResolvedMovieIdentity(
                        "movie",
                        movieId,
                        1,
                        "Arrival",
                        2016,
                        116,
                        "Arrival",
                        "Overview",
                        null,
                        null,
                        new DateOnly(2016, 1, 1),
                        8m,
                        100,
                        ["Science Fiction"]),
                    "Mind-bending"),
                new AiValidatedRecommendation(
                    new ResolvedMovieIdentity(
                        "tv",
                        tvShowId,
                        1396,
                        "Breaking Bad",
                        2008,
                        null,
                        "Breaking Bad",
                        "Overview",
                        null,
                        null,
                        new DateOnly(2008, 1, 20),
                        9m,
                        1000,
                        ["Drama"]),
                    "Intense psychological series")
            ],
            new AiValidationResult([], 2, 2, 0, false));

        var response = AiRecommendationContractMapper.ToResponse(result);

        Assert.Equal("movie", response.Recommendations[0].Type);
        Assert.Equal("tv", response.Recommendations[1].Type);
        Assert.Equal(movieId, response.Recommendations[0].Id);
        Assert.Equal(tvShowId, response.Recommendations[1].Id);
    }
}
