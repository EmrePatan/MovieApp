using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakePersonDataProvider : IPersonDataProvider
{
    public const int McConaugheyTmdbId = 1001;
    public const int CranstonTmdbId = 2001;
    public const int KeanuReevesTmdbId = 3001;
    public const int ChristopherNolanTmdbId = 3002;
    public const int ScarlettJohanssonTmdbId = 3003;

    private static readonly IReadOnlyList<PersonProviderSummary> SearchCatalog =
    [
        new(KeanuReevesTmdbId, "Keanu Reeves", "/fake/keanu.jpg", "Acting", 85.4m),
        new(ChristopherNolanTmdbId, "Christopher Nolan", "/fake/nolan.jpg", "Directing", 72.1m),
        new(ScarlettJohanssonTmdbId, "Scarlett Johansson", "/fake/scarlett.jpg", "Acting", 68.9m),
        new(McConaugheyTmdbId, "Matthew McConaughey", "/fake/cooper.jpg", "Acting", 55.2m),
        new(CranstonTmdbId, "Bryan Cranston", "/fake/walter.jpg", "Acting", 49.7m)
    ];

    public Task<PersonProviderSearchResult> SearchPersonsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        var matches = SearchCatalog
            .Where(summary => summary.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var skip = Math.Max(0, (page - 1) * pageSize);
        var pageItems = matches.Skip(skip).Take(pageSize).ToList();
        var totalPages = matches.Count == 0 ? 0 : (int)Math.Ceiling(matches.Count / (double)pageSize);

        return Task.FromResult(new PersonProviderSearchResult(
            pageItems,
            page,
            pageSize,
            matches.Count,
            totalPages));
    }

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

        if (tmdbPersonId == KeanuReevesTmdbId)
        {
            return Task.FromResult<PersonProviderDetails?>(CreateKeanu());
        }

        if (tmdbPersonId == ChristopherNolanTmdbId)
        {
            return Task.FromResult<PersonProviderDetails?>(CreateNolan());
        }

        return Task.FromResult<PersonProviderDetails?>(null);
    }

    private static PersonProviderDetails CreateKeanu() =>
        new(
            KeanuReevesTmdbId,
            "Keanu Reeves",
            "/fake/keanu.jpg",
            "A Canadian actor known for action and sci-fi roles.",
            new DateOnly(1964, 9, 2),
            null,
            "Beirut, Lebanon",
            "Acting",
            []);

    private static PersonProviderDetails CreateNolan() =>
        new(
            ChristopherNolanTmdbId,
            "Christopher Nolan",
            "/fake/nolan.jpg",
            "A British-American filmmaker known for complex narratives.",
            new DateOnly(1970, 7, 30),
            null,
            "London, England, UK",
            "Directing",
            []);

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
