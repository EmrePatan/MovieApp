using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.CatalogFollows;

public sealed class CatalogUpcomingTvPremiereDedupTests
{
    private static readonly Guid ShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherShowId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid MovieId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateOnly SharedDate = new(2026, 10, 10);
    private static readonly DateOnly OtherDate = new(2026, 10, 11);

    [Fact]
    public void Apply_SameShowPremiereAndSeasonOneEpisodeOneOnSameDate_KeepsEpisodeOnly()
    {
        var items = new[]
        {
            Premiere(ShowId, SharedDate),
            Episode(ShowId, SharedDate, 1, 1)
        };

        var result = CatalogUpcomingTvPremiereDedup.Apply(items);

        var episode = Assert.Single(result);
        Assert.Equal(CatalogUpcomingKind.TvEpisode, episode.UpcomingKind);
        Assert.Equal(1, episode.SeasonNumber);
        Assert.Equal(1, episode.EpisodeNumber);
    }

    [Fact]
    public void Apply_PremiereWithoutSeasonOneEpisodeOne_KeepsPremiere()
    {
        var items = new[] { Premiere(ShowId, SharedDate) };

        var premiere = Assert.Single(CatalogUpcomingTvPremiereDedup.Apply(items));
        Assert.Equal(CatalogUpcomingKind.TvShowPremiere, premiere.UpcomingKind);
    }

    [Fact]
    public void Apply_SeasonOneEpisodeOneWithNullEligibilityViaMissingEpisodeRow_KeepsPremiere()
    {
        var items = new[]
        {
            Premiere(ShowId, SharedDate),
            Episode(ShowId, SharedDate, 1, 2)
        };

        Assert.Equal(2, CatalogUpcomingTvPremiereDedup.Apply(items).Count);
    }

    [Fact]
    public void Apply_PremiereAndSeasonOneEpisodeOneOnDifferentDates_KeepsBoth()
    {
        var items = new[]
        {
            Premiere(ShowId, SharedDate),
            Episode(ShowId, OtherDate, 1, 1)
        };

        Assert.Equal(2, CatalogUpcomingTvPremiereDedup.Apply(items).Count);
    }

    [Fact]
    public void Apply_DifferentShowsOnSameDate_KeepsBothPremieres()
    {
        var items = new[]
        {
            Premiere(ShowId, SharedDate),
            Premiere(OtherShowId, SharedDate),
            Episode(ShowId, SharedDate, 1, 1),
            Episode(OtherShowId, SharedDate, 1, 1)
        };

        var result = CatalogUpcomingTvPremiereDedup.Apply(items);

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.Equal(CatalogUpcomingKind.TvEpisode, item.UpcomingKind));
    }

    [Fact]
    public void Apply_MovieRelease_IsUnaffected()
    {
        var items = new[]
        {
            Movie(MovieId, SharedDate),
            Premiere(ShowId, SharedDate),
            Episode(ShowId, SharedDate, 1, 1)
        };

        var result = CatalogUpcomingTvPremiereDedup.Apply(items);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.UpcomingKind == CatalogUpcomingKind.MovieRelease);
        Assert.DoesNotContain(result, item => item.UpcomingKind == CatalogUpcomingKind.TvShowPremiere);
    }

    [Fact]
    public void Apply_PreservesRelativeOrderingOfRemainingItems()
    {
        var items = new[]
        {
            Movie(MovieId, SharedDate),
            Premiere(ShowId, SharedDate),
            Episode(ShowId, SharedDate, 1, 1),
            Episode(OtherShowId, OtherDate, 1, 1)
        };

        var result = CatalogUpcomingTvPremiereDedup.Apply(items);

        Assert.Equal(
            [MovieId, ShowId, OtherShowId],
            result.Select(item => item.ContentId).ToList());
    }

    private static CatalogUpcomingItemResult Premiere(Guid contentId, DateOnly releaseDate) =>
        new(
            contentId,
            CatalogContentType.Tv,
            CatalogUpcomingKind.TvShowPremiere,
            "Show",
            null,
            releaseDate,
            true,
            null,
            null,
            null,
            null);

    private static CatalogUpcomingItemResult Episode(
        Guid contentId,
        DateOnly releaseDate,
        int season,
        int episode) =>
        new(
            contentId,
            CatalogContentType.Tv,
            CatalogUpcomingKind.TvEpisode,
            "Show",
            null,
            releaseDate,
            true,
            Guid.NewGuid(),
            season,
            episode,
            "Episode");

    private static CatalogUpcomingItemResult Movie(Guid contentId, DateOnly releaseDate) =>
        new(
            contentId,
            CatalogContentType.Movie,
            CatalogUpcomingKind.MovieRelease,
            "Movie",
            null,
            releaseDate,
            true,
            null,
            null,
            null,
            null);
}
