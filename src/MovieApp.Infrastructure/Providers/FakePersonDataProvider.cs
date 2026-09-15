using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakePersonDataProvider : IPersonDataProvider
{
    public const int McConaugheyTmdbId = 1001;
    public const int CranstonTmdbId = 2001;

    public Task<PersonProviderDetails?> GetPersonAsync(int tmdbPersonId, CancellationToken cancellationToken = default)
    {
        if (tmdbPersonId == McConaugheyTmdbId)
        {
            return Task.FromResult<PersonProviderDetails?>(CreateMcConaughey());
        }

        if (tmdbPersonId == CranstonTmdbId)
        {
            return Task.FromResult<PersonProviderDetails?>(CreateCranston());
        }

        return Task.FromResult<PersonProviderDetails?>(null);
    }

    private static PersonProviderDetails CreateMcConaughey() =>
        new(
            McConaugheyTmdbId,
            "Matthew McConaughey",
            "/fake/cooper.jpg",
            "An Academy Award-winning actor known for dramatic and charismatic performances.",
            new DateOnly(1969, 11, 4),
            null,
            "Uvalde, Texas, USA",
            "Acting",
            [
                new PersonFilmographyCredit(
                    "movie",
                    FakeMovieDataProvider.InterstellarTmdbId,
                    "Interstellar",
                    "/fake/interstellar-poster.jpg",
                    "Cooper",
                    new DateOnly(2014, 11, 7),
                    120.5m,
                    8.6m),
                new PersonFilmographyCredit(
                    "movie",
                    FakeMovieDataProvider.InterstellarTmdbId,
                    "Interstellar",
                    "/fake/interstellar-poster.jpg",
                    "Cooper",
                    new DateOnly(2014, 11, 7),
                    120.5m,
                    8.6m),
                new PersonFilmographyCredit(
                    "tv",
                    999001,
                    "Untitled Series",
                    null,
                    "Host",
                    null,
                    5.0m,
                    6.0m)
            ]);

    private static PersonProviderDetails CreateCranston() =>
        new(
            CranstonTmdbId,
            "Bryan Cranston",
            "/fake/walter.jpg",
            "An Emmy-winning actor best known for transformative television roles.",
            new DateOnly(1956, 3, 7),
            null,
            "Hollywood, California, USA",
            "Acting",
            [
                new PersonFilmographyCredit(
                    "tv",
                    FakeTvShowDataProvider.BreakingBadTmdbId,
                    "Breaking Bad",
                    "/fake/breaking-bad-poster.jpg",
                    "Walter White",
                    new DateOnly(2008, 1, 20),
                    95.0m,
                    9.5m)
            ]);
}
