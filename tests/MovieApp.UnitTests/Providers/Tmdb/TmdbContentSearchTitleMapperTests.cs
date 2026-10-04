using System.Text.Json;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbContentSearchTitleMapperTests
{
    private static readonly JsonSerializerOptions TmdbSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void MapMovie_DeserializesTranslationAndAlternativeIsoMetadataFromTmdbJson()
    {
        const string json = """
            {
              "id": 1417,
              "title": "Pan's Labyrinth",
              "alternative_titles": {
                "titles": [
                  {
                    "iso_3166_1": "TR",
                    "title": "Pan'ın Labirenti",
                    "type": "working"
                  }
                ]
              },
              "translations": {
                "translations": [
                  {
                    "iso_3166_1": "TR",
                    "iso_639_1": "tr",
                    "data": { "title": "Pan'ın Labirenti" }
                  }
                ]
              }
            }
            """;

        var details = JsonSerializer.Deserialize<TmdbMovieDetailsResponseJson>(json, TmdbSerializerOptions);
        Assert.NotNull(details);

        var mapped = TmdbContentSearchTitleMapper.MapMovie(details);

        var translation = Assert.Single(mapped, entry => entry.Source == ContentSearchTitleSource.TmdbTranslation);
        Assert.Equal("tr", translation.LanguageCode);
        Assert.Equal("TR", translation.CountryCode);
        Assert.Equal("Pan'ın Labirenti", translation.Title);

        var alternative = Assert.Single(mapped, entry => entry.Source == ContentSearchTitleSource.TmdbAlternative);
        Assert.Null(alternative.LanguageCode);
        Assert.Equal("TR", alternative.CountryCode);
        Assert.Equal("working", alternative.ProviderTitleType);
    }

    [Fact]
    public void MapTvShow_DeserializesTranslationIsoMetadataFromTmdbJson()
    {
        const string json = """
            {
              "id": 1,
              "name": "Show",
              "alternative_titles": {
                "results": [
                  { "iso_3166_1": "US", "title": "Alias", "type": "working" }
                ]
              },
              "translations": {
                "translations": [
                  {
                    "iso_3166_1": "TR",
                    "iso_639_1": "tr",
                    "data": { "name": "Dizi" }
                  }
                ]
              }
            }
            """;

        var details = JsonSerializer.Deserialize<TmdbTvDetailsResponseJson>(json, TmdbSerializerOptions);
        Assert.NotNull(details);

        var mapped = TmdbContentSearchTitleMapper.MapTvShow(details);

        var translation = Assert.Single(mapped, entry => entry.Source == ContentSearchTitleSource.TmdbTranslation);
        Assert.Equal("tr", translation.LanguageCode);
        Assert.Equal("TR", translation.CountryCode);
        Assert.Equal("Dizi", translation.Title);

        var alternative = Assert.Single(mapped, entry => entry.Source == ContentSearchTitleSource.TmdbAlternative);
        Assert.Equal("US", alternative.CountryCode);
    }

    [Fact]
    public void MapMovie_RejectsNonIsoCountryNamesFromAlternativeTitles()
    {
        const string json = """
            {
              "id": 635396,
              "title": "Where on Earth Is Katy Manning?",
              "alternative_titles": {
                "titles": [
                  {
                    "iso_3166_1": "UNITED KINGDOM",
                    "title": "Where on Earth... is Katy Manning cause she'd really like to know!",
                    "type": "working"
                  },
                  {
                    "iso_3166_1": "GB",
                    "title": "UK Title",
                    "type": "working"
                  }
                ]
              }
            }
            """;

        var details = JsonSerializer.Deserialize<TmdbMovieDetailsResponseJson>(json, TmdbSerializerOptions);
        Assert.NotNull(details);

        var mapped = TmdbContentSearchTitleMapper.MapMovie(details);

        var longCountry = Assert.Single(
            mapped,
            entry => entry.Title.Contains("Katy Manning cause", StringComparison.Ordinal));
        Assert.Null(longCountry.CountryCode);

        var isoCountry = Assert.Single(mapped, entry => entry.Title == "UK Title");
        Assert.Equal("GB", isoCountry.CountryCode);
    }

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
