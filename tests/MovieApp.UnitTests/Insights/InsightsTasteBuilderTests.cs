using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsTasteBuilderTests
{
    private static readonly Guid ActionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DramaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ComedyId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void BuildReturnsEmptyWhenFewerThanThreeEligibleTitles()
    {
        var raw = CreateRaw(
        [
            Title(2020, [Genre(ActionId, "Action")]),
            Title(2021, []),
        ]);

        var result = InsightsTasteBuilder.Build(raw);

        Assert.Empty(result.Genres);
    }

    [Fact]
    public void BuildUsesFractionalMultiGenreWeighting()
    {
        var raw = CreateRaw(
        [
            Title(2020, [Genre(ActionId, "Action"), Genre(DramaId, "Drama")]),
            Title(2021, [Genre(ActionId, "Action")]),
            Title(2022, [Genre(ComedyId, "Comedy")]),
        ]);

        var result = InsightsTasteBuilder.Build(raw);
        var action = result.Genres.Single(genre => genre.GenreId == ActionId);

        Assert.Equal(1.5m, action.Weight);
        Assert.Equal(50.0m, action.SharePercent);
    }

    [Fact]
    public void BuildReturnsTopSixGenresWithDeterministicTieBreak()
    {
        var raw = CreateRaw(
            Enumerable.Range(1, 8)
                .Select(index => Title(2000 + index, [Genre(Guid.Parse($"dddddddd-dddd-dddd-dddd-{index:D12}"), $"Genre {index}")]))
                .ToList());

        var result = InsightsTasteBuilder.Build(raw);

        Assert.Equal(6, result.Genres.Count);
        Assert.Equal("Genre 1", result.Genres[0].Name);
        Assert.Equal("Genre 6", result.Genres[^1].Name);
    }

    private static InsightsDnaTitleData Title(int year, IReadOnlyList<InsightsDnaGenreData> genres) =>
        new(year, genres);

    private static InsightsDnaGenreData Genre(Guid id, string name) => new(id, name);

    private static InsightsAnalyticsRawData CreateRaw(List<InsightsDnaTitleData> movieTitles) =>
        new(
            DateTime.UtcNow,
            movieTitles.Count,
            0,
            0,
            0,
            [],
            movieTitles,
            [],
            0,
            0,
            0,
            0,
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
}
