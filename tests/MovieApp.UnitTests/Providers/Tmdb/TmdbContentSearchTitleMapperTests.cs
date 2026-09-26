using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbContentSearchTitleMapperTests
{
    [Fact]
    public void MapTvShow_TruncatesLongAlternativeTitleTypeToDatabaseLimit()
    {
        var longType = new string('x', 80);
        var details = new TmdbTvDetailsResponseJson
        {
            Id = 1,
            Name = "Show",
            AlternativeTitles = new TmdbTvAlternativeTitlesAppendJson
            {
                Results =
                [
                    new TmdbAlternativeTitleJson
                    {
                        Title = "Alias",
                        Iso31661 = "US",
                        Type = longType,
                    },
                ],
            },
        };

        var mapped = TmdbContentSearchTitleMapper.MapTvShow(details);

        Assert.Single(mapped);
        Assert.Equal(64, mapped[0].ProviderTitleType!.Length);
        Assert.Equal(longType[..64], mapped[0].ProviderTitleType);
        Assert.Equal(ContentSearchTitleSource.TmdbAlternative, mapped[0].Source);
    }
}
