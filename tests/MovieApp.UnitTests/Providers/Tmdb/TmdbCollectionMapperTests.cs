using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbCollectionMapperTests
{
    [Fact]
    public void ToCollectionProviderDetailsMapsCollectionAndParts()
    {
        var details = TmdbCollectionMapper.ToCollectionProviderDetails(new TmdbCollectionResponseJson
        {
            Id = 645,
            Name = "James Bond Collection",
            Overview = "Overview",
            PosterPath = "poster.jpg",
            BackdropPath = "/backdrop.jpg",
            Parts =
            [
                new TmdbCollectionPartJson
                {
                    Id = 707,
                    Title = "Dr. No",
                    OriginalTitle = "Dr. No",
                    Overview = "Part overview",
                    PosterPath = "/part-poster.jpg",
                    BackdropPath = "/part-backdrop.jpg",
                    ReleaseDate = "1962-10-05",
                    VoteAverage = 7.0m,
                    VoteCount = 1000,
                    Adult = false
                }
            ]
        });

        Assert.Equal(645, details.TmdbId);
        Assert.Equal("James Bond Collection", details.Name);
        Assert.Equal("/poster.jpg", details.PosterPath);
        Assert.Single(details.Parts);
        Assert.Equal(707, details.Parts[0].TmdbId);
        Assert.Equal(new DateOnly(1962, 10, 5), details.Parts[0].ReleaseDate);
        Assert.False(details.Parts[0].Adult);
    }
}
