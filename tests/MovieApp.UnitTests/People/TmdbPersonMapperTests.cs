using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.People;

public sealed class TmdbPersonMapperTests
{
    [Fact]
    public void MapsPersonDetailsAndActingFilmography()
    {
        var person = new TmdbPersonJson
        {
            Id = 42,
            Name = "Jane Actor",
            Biography = "  A celebrated performer.  ",
            Birthday = "1980-05-10",
            PlaceOfBirth = "Paris, France",
            ProfilePath = "/profile.jpg",
            KnownForDepartment = "Acting"
        };

        var credits = new TmdbCombinedCreditsResponseJson
        {
            Cast =
            [
                new TmdbCombinedCreditCastJson
                {
                    Id = 100,
                    MediaType = "movie",
                    Title = "Star Film",
                    Character = "Lead",
                    PosterPath = "/poster.jpg",
                    ReleaseDate = "2020-01-01"
                },
                new TmdbCombinedCreditCastJson
                {
                    Id = 200,
                    MediaType = "tv",
                    Name = "Series One",
                    Character = "Guest",
                    FirstAirDate = "2019-06-01"
                },
                new TmdbCombinedCreditCastJson
                {
                    Id = 0,
                    MediaType = "movie",
                    Title = "Broken",
                    Character = "Nobody"
                },
                new TmdbCombinedCreditCastJson
                {
                    Id = 300,
                    MediaType = "movie",
                    Title = "Crew Only",
                    Character = null
                }
            ]
        };

        var result = TmdbPersonMapper.ToPersonProviderDetails(person, credits);

        Assert.Equal(42, result.TmdbId);
        Assert.Equal("Jane Actor", result.Name);
        Assert.Equal("A celebrated performer.", result.Biography);
        Assert.Equal(new DateOnly(1980, 5, 10), result.Birthday);
        Assert.Equal("Acting", result.KnownForDepartment);
        Assert.Equal(2, result.FilmographyCredits.Count);
        Assert.Contains(result.FilmographyCredits, credit => credit.MediaType == "movie" && credit.TmdbId == 100);
        Assert.Contains(result.FilmographyCredits, credit => credit.MediaType == "tv" && credit.TmdbId == 200);
    }
}
