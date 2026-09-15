using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Collections;

namespace MovieApp.UnitTests.Collections;

public sealed class CollectionPartPolicyTests
{
    [Fact]
    public void ApplyRemovesAdultParts()
    {
        var parts = new[]
        {
            CreatePart(1, "Alpha", new DateOnly(2010, 1, 1), adult: false),
            CreatePart(2, "Adult", new DateOnly(2011, 1, 1), adult: true)
        };

        var result = CollectionPartPolicy.Apply(parts);

        Assert.Single(result);
        Assert.Equal(1, result[0].TmdbId);
    }

    [Fact]
    public void ApplyDedupesByTmdbIdKeepingFirstOccurrence()
    {
        var parts = new[]
        {
            CreatePart(10, "First", new DateOnly(2010, 1, 1), adult: false),
            CreatePart(10, "Duplicate", new DateOnly(2012, 1, 1), adult: false)
        };

        var result = CollectionPartPolicy.Apply(parts);

        Assert.Single(result);
        Assert.Equal("First", result[0].Title);
    }

    [Fact]
    public void ApplyKeepsFutureReleaseDates()
    {
        var parts = new[]
        {
            CreatePart(20, "Future", new DateOnly(2099, 1, 1), adult: false)
        };

        var result = CollectionPartPolicy.Apply(parts);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2099, 1, 1), result[0].ReleaseDate);
    }

    [Fact]
    public void ApplyOrdersByReleaseDateThenTitleThenTmdbId()
    {
        var parts = new[]
        {
            CreatePart(30, "Bravo", new DateOnly(2015, 1, 1), adult: false),
            CreatePart(10, "Alpha", new DateOnly(2010, 1, 1), adult: false),
            CreatePart(20, "Charlie", null, adult: false),
            CreatePart(15, "Delta", new DateOnly(2010, 1, 1), adult: false)
        };

        var result = CollectionPartPolicy.Apply(parts);

        Assert.Equal([10, 15, 30, 20], result.Select(part => part.TmdbId).ToArray());
    }

    private static CollectionProviderPart CreatePart(
        int tmdbId,
        string title,
        DateOnly? releaseDate,
        bool adult) =>
        new(
            tmdbId,
            title,
            title,
            null,
            null,
            null,
            releaseDate,
            7.0m,
            100,
            adult);
}
