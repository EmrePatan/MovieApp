using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class ProviderSearchMapperIntentRankingTests
{
    [Fact]
    public void MergeProviderResults_AllSearch_RanksExactPersonTomHanksFirst()
    {
        var result = MergeAll(
            "Tom Hanks",
            movies:
            [
                Movie(1128918, "Tom Hanks: The Nomad", popularity: 12m),
            ],
            persons:
            [
                Person(31, "Tom Hanks", 45m),
            ]);

        Assert.Equal("person", result.Items[0].Type);
        Assert.Equal(31, result.Items[0].TmdbId);
        Assert.Equal("Tom Hanks", result.Items[0].Title);
    }

    [Fact]
    public void MergeProviderResults_AllSearch_ExactPersonBeatsTomHanksTitlePrefixMovie()
    {
        var result = MergeAll(
            "Tom Hanks",
            movies:
            [
                Movie(1, "Tom Hanks: The Nomad", popularity: 99m),
                Movie(2, "Tom Hanks: A League of His Own", popularity: 98m),
            ],
            persons:
            [
                Person(31, "Tom Hanks", 40m),
            ]);

        Assert.Equal("person", result.Items[0].Type);
        Assert.Equal(31, result.Items[0].TmdbId);
    }

    [Fact]
    public void MergeProviderResults_AllSearch_DisambiguatesChristopherNolanHomonymsByPopularity()
    {
        var result = MergeAll(
            "Christopher Nolan",
            persons:
            [
                Person(6279344, "Christopher Nolan", 1m),
                Person(525, "Christopher Nolan", 72m),
                Person(4066940, "Christopher Nolan", 2m),
            ]);

        Assert.Equal(525, result.Items[0].TmdbId);
        Assert.Equal(3, result.Items.Count(item => item.Type == "person"));
    }

    [Fact]
    public void MergeProviderResults_AllSearch_KeepsCatalogAheadOfPersonBatman()
    {
        var result = MergeAll(
            "Batman",
            movies:
            [
                Movie(268, "Batman", popularity: 10m),
            ],
            tv:
            [
                Tv(2287, "Batman", popularity: 20m),
            ],
            persons:
            [
                Person(5490404, "Batman", 50m),
            ]);

        Assert.Equal("tv", result.Items[0].Type);
        Assert.Equal(2287, result.Items[0].TmdbId);
        Assert.DoesNotContain(result.Items.Take(2), item => item.Type == "person");
    }

    [Fact]
    public void MergeProviderResults_AllSearch_KeepsHarryPotterCatalogAheadOfPersonCollisions()
    {
        var result = MergeAll(
            "Harry Potter",
            movies:
            [
                Movie(671, "Harry Potter and the Philosopher's Stone", popularity: 180m),
            ],
            tv:
            [
                Tv(224377, "Harry Potter", popularity: 5m),
            ],
            persons:
            [
                Person(3001, "Harry Potter", 4m),
                Person(3002, "Harry Potter", 3m),
            ]);

        Assert.Equal("tv", result.Items[0].Type);
        Assert.Equal(224377, result.Items[0].TmdbId);
        Assert.Equal("movie", result.Items[1].Type);
        Assert.True(result.Items.Select((item, index) => (item, index))
            .Where(pair => pair.item.Type == "person")
            .Select(pair => pair.index)
            .DefaultIfEmpty(-1)
            .First() > 0);
    }

    [Fact]
    public void MergeProviderResults_AllSearch_RanksInterstellarMovieFirst()
    {
        var result = MergeAll(
            "Interstellar",
            movies:
            [
                Movie(157336, "Interstellar", popularity: 100m),
                Movie(301959, "Interstellar: Nolan's Odyssey", popularity: 5m),
            ]);

        Assert.Equal("movie", result.Items[0].Type);
        Assert.Equal(157336, result.Items[0].TmdbId);
    }

    [Fact]
    public void MergeProviderResults_AllSearch_RanksFriendsTvFirst()
    {
        var result = MergeAll(
            "Friends",
            tv:
            [
                Tv(1668, "Friends", popularity: 100m),
            ],
            movies:
            [
                Movie(50544, "Friends with Benefits", popularity: 50m),
            ]);

        Assert.Equal("tv", result.Items[0].Type);
        Assert.Equal(1668, result.Items[0].TmdbId);
    }

    [Fact]
    public void MergeProviderResults_AllSearch_MatchesOriginalTitleForEnglishQuery()
    {
        var result = MergeAll(
            "Interstellar",
            movies:
            [
                Movie(
                    157336,
                    "Yıldızlararası",
                    originalTitle: "Interstellar",
                    popularity: 100m),
            ]);

        Assert.Equal(157336, result.Items[0].TmdbId);
    }

    [Fact]
    public void MergeProviderResults_AllSearch_MatchesLocalizedDisplayTitle()
    {
        var result = MergeAll(
            "Yıldızlararası",
            movies:
            [
                Movie(
                    157336,
                    "Yıldızlararası",
                    originalTitle: "Interstellar",
                    popularity: 100m),
            ]);

        Assert.Equal(157336, result.Items[0].TmdbId);
    }

    [Fact]
    public void MergeProviderResults_AllSearch_DoesNotBoostWeakPersonOverStrongCatalog()
    {
        var result = MergeAll(
            "Inception",
            movies:
            [
                Movie(27205, "Inception", popularity: 120m),
            ],
            persons:
            [
                Person(9001, "Inception Extra", 1m),
            ]);

        Assert.Equal("movie", result.Items[0].Type);
        Assert.Equal(27205, result.Items[0].TmdbId);
    }

    [Fact]
    public void MergeProviderResults_MovieSearch_IsUnchangedByPersonIntentRules()
    {
        var criteria = Criteria("Inception", SearchContentType.Movie);
        var movieResult = new MovieProviderSearchResult(
            [Movie(27205, "Inception", popularity: 100m)],
            1,
            20,
            1,
            1);
        var movieIds = new Dictionary<int, Guid> { [27205] = Guid.NewGuid() };

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            movieResult,
            null,
            null,
            movieIds,
            new Dictionary<int, Guid>(),
            new Dictionary<int, Guid>());

        Assert.Equal("Inception", result.Items[0].Title);
    }

    [Fact]
    public void MergeProviderResults_PersonSearch_StillSortsByRelevanceAndPopularity()
    {
        var criteria = Criteria("christopher", SearchContentType.Person);
        var personResult = new PersonProviderSearchResult(
            [
                new(3005, "Other Nolan", null, "Acting", 99m),
                new(3002, "Christopher Nolan", null, "Directing", 72m),
            ],
            1,
            20,
            2,
            1);
        var personIds = personResult.Results.ToDictionary(summary => summary.TmdbId, _ => Guid.NewGuid());

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            null,
            null,
            personResult,
            new Dictionary<int, Guid>(),
            new Dictionary<int, Guid>(),
            personIds);

        Assert.Equal("Christopher Nolan", result.Items[0].Title);
    }

    [Fact]
    public void MergeAutocompleteSuggestions_UsesImprovedRankingForTomHanks()
    {
        var movieResult = new MovieProviderSearchResult(
            [Movie(1128918, "Tom Hanks: The Nomad", popularity: 90m)],
            1,
            20,
            1,
            1);
        var tvResult = new TvShowProviderSearchResult([], 1, 20, 0, 0);
        var personResult = new PersonProviderSearchResult(
            [Person(31, "Tom Hanks", 40m)],
            1,
            20,
            1,
            1);

        var movieIds = new Dictionary<int, Guid> { [1128918] = Guid.NewGuid() };
        var personIds = new Dictionary<int, Guid> { [31] = Guid.NewGuid() };

        var suggestions = ProviderSearchMapper.MergeAutocompleteSuggestions(
            "Tom Hanks",
            movieResult,
            tvResult,
            personResult,
            movieIds,
            new Dictionary<int, Guid>(),
            personIds,
            10);

        Assert.Equal("person", suggestions[0].Type);
        Assert.Equal(31, suggestions[0].TmdbId);
    }

    private static PaginatedResult<SearchItem> MergeAll(
        string query,
        IReadOnlyList<MovieProviderSummary>? movies = null,
        IReadOnlyList<TvShowProviderSummary>? tv = null,
        IReadOnlyList<PersonProviderSummary>? persons = null)
    {
        movies ??= [];
        tv ??= [];
        persons ??= [];

        var movieIds = movies
            .Where(movie => movie.TmdbId.HasValue)
            .ToDictionary(movie => movie.TmdbId!.Value, _ => Guid.NewGuid());
        var tvIds = tv
            .Where(show => show.TmdbId.HasValue)
            .ToDictionary(show => show.TmdbId!.Value, _ => Guid.NewGuid());
        var personIds = persons.ToDictionary(person => person.TmdbId, _ => Guid.NewGuid());

        return ProviderSearchMapper.MergeProviderResults(
            Criteria(query, SearchContentType.All),
            new MovieProviderSearchResult(movies, 1, 20, movies.Count, 1),
            new TvShowProviderSearchResult(tv, 1, 20, tv.Count, 1),
            new PersonProviderSearchResult(persons, 1, 20, persons.Count, 1),
            movieIds,
            tvIds,
            personIds);
    }

    private static SearchCriteria Criteria(string query, SearchContentType type) =>
        new(query, type, null, null, null, null, SearchSortOption.Relevance, 1, 20);

    private static MovieProviderSummary Movie(
        int tmdbId,
        string title,
        string? originalTitle = null,
        decimal popularity = 10m) =>
        new(
            $"tmdb:movie:{tmdbId}",
            tmdbId,
            null,
            null,
            title,
            null,
            null,
            null,
            8m,
            1000,
            originalTitle,
            popularity);

    private static TvShowProviderSummary Tv(int tmdbId, string title, decimal popularity = 10m) =>
        new(
            $"tmdb:tv:{tmdbId}",
            tmdbId,
            null,
            null,
            title,
            title,
            null,
            null,
            null,
            null,
            null,
            8m,
            500,
            popularity);

    private static PersonProviderSummary Person(int tmdbId, string name, decimal popularity) =>
        new(tmdbId, name, null, "Acting", popularity);
}
