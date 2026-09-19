using System.Text.Json;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class PersonDetailLocalizationSerializationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PersonDetailLocalizationData_RoundTripsThroughRedisSerializer()
    {
        var original = new PersonDetailLocalizationData(
            Biography: "Turkish biography",
            Filmography:
            [
                new PersonFilmographyLocalizationItem("movie", 550, "Fight Club", "The Narrator"),
                new PersonFilmographyLocalizationItem("tv", 1399, "Game of Thrones", "Tyrion Lannister"),
            ]);

        var cacheEntry = new DetailLocalizationCacheEntry<PersonDetailLocalizationData> { Data = original };
        var json = JsonSerializer.Serialize(cacheEntry, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<DetailLocalizationCacheEntry<PersonDetailLocalizationData>>(
            json,
            SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Data);
        Assert.Equal(original.Biography, deserialized.Data.Biography);
        Assert.Equal(2, deserialized.Data.Filmography.Count);
        Assert.Contains(
            deserialized.Data.Filmography,
            item => item.MediaType == "movie" && item.TmdbId == 550 && item.Title == "Fight Club");
        Assert.Contains(
            deserialized.Data.Filmography,
            item => item.MediaType == "tv" && item.TmdbId == 1399 && item.Character == "Tyrion Lannister");
    }
}
