using System.Text.Json;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbOriginCountryMapperTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void ProductionCountryJson_BindsTmdbIso31661()
    {
        var parsed = JsonSerializer.Deserialize<TmdbProductionCountryJson>(
            """{"iso_3166_1":"TR","name":"Turkey"}""",
            SerializerOptions);

        Assert.NotNull(parsed);
        Assert.Equal("TR", parsed.Iso31661);
    }

    [Fact]
    public void MovieDetails_MapTrProductionCountry_WhenOriginArrayMissing()
    {
        var details = JsonSerializer.Deserialize<TmdbMovieDetailsResponseJson>(
            """
            {
              "id": 1,
              "title": "Alev Alev",
              "production_countries": [{ "iso_3166_1": "TR", "name": "Turkey" }]
            }
            """,
            SerializerOptions);

        var mapped = TmdbMovieMapper.ToDetails(details!);

        Assert.Equal("TR", mapped.PrimaryOriginCountryCode);
    }

    [Fact]
    public void ResolvePrimaryOriginCountryCode_PrefersTurkeyAndSkipsInvalidCodes()
    {
        var code = TmdbOriginCountryMapper.ResolvePrimaryOriginCountryCode(
            [null, "USA", " ", "de", "tr"]);

        Assert.Equal("TR", code);
    }

    [Fact]
    public void ResolvePrimaryOriginCountryCode_ReturnsNullWhenNoTwoLetterCodeExists()
    {
        Assert.Null(TmdbOriginCountryMapper.ResolvePrimaryOriginCountryCode([null, "Turkey", ""]));
    }
}
