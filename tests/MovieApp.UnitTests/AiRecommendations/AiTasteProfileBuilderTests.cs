using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiTasteProfileBuilderTests
{
    [Fact]
    public void BuildFromRawTreatsRatingsAtLeastEightAsPositive()
    {
        var raw = new AiTasteProfileRawData(
            [new AiRatingRow(Guid.NewGuid(), "Inception", 2010, ["Science Fiction"], 9)],
            [],
            [],
            [],
            []);

        var profile = AiTasteProfileBuilder.BuildFromRaw(raw);

        Assert.Single(profile.HighRatings);
        Assert.Equal("Inception", profile.HighRatings[0].Title);
        Assert.Empty(profile.LowRatings);
    }

    [Fact]
    public void BuildFromRawTreatsRatingsAtMostFiveAsNegative()
    {
        var raw = new AiTasteProfileRawData(
            [new AiRatingRow(Guid.NewGuid(), "Bad Movie", 2020, ["Horror"], 4)],
            [],
            [],
            [],
            []);

        var profile = AiTasteProfileBuilder.BuildFromRaw(raw);

        Assert.Single(profile.LowRatings);
        Assert.Empty(profile.HighRatings);
        Assert.Contains(profile.AvoidedGenres, genre => genre.Genre == "Horror");
    }

    [Fact]
    public void BuildFromRawDoesNotTreatWatchedMoviesAsPositive()
    {
        var raw = new AiTasteProfileRawData(
            [],
            [],
            [],
            [new AiWatchedMovieRow(Guid.NewGuid(), "Watched Movie", 2015, ["Drama"])],
            []);

        var profile = AiTasteProfileBuilder.BuildFromRaw(raw);

        Assert.Empty(profile.HighRatings);
        Assert.Empty(profile.Favorites);
        Assert.DoesNotContain(profile.TopGenres, genre => genre.Genre == "Drama");
    }

    [Fact]
    public void BuildFromRawEnforcesBounds()
    {
        var ratings = Enumerable.Range(1, 20)
            .Select(index => new AiRatingRow(Guid.NewGuid(), $"Movie {index}", 2000 + index, ["Action"], 9))
            .ToList();

        var raw = new AiTasteProfileRawData(ratings, [], [], [], []);

        var profile = AiTasteProfileBuilder.BuildFromRaw(raw);

        Assert.Equal(10, profile.HighRatings.Count);
    }

    [Fact]
    public void BuildFromRawProducesColdStartProfileWhenEmpty()
    {
        var raw = new AiTasteProfileRawData([], [], [], [], []);

        var profile = AiTasteProfileBuilder.BuildFromRaw(raw);

        Assert.True(profile.IsColdStart);
        Assert.Empty(profile.HighRatings);
        Assert.Empty(profile.Favorites);
    }
}
