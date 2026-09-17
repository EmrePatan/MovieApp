using MovieApp.Application.Models.Recommendations;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Recommendations;

[Collection("CatalogPersistence")]
public sealed class PersonalizedCandidateKeywordIntegrationTests
{
    [Fact]
    public async Task ConsolidatedPersonalizedCandidatesIncludeKeywordIds()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);
        var genre = CreateGenre("Action");
        var keyword = CreateKeyword();
        var movie = CreateMovie(tmdbId: NextTmdbId());

        context.Genres.Add(genre);
        context.Keywords.Add(keyword);
        context.Movies.Add(movie);
        context.MovieGenres.Add(LinkMovieGenre(movie, genre));
        context.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Movie = movie,
            Keyword = keyword
        });
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.Movie,
            [genre.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 10);

        var candidate = Assert.Single(candidates);
        Assert.Equal(movie.Id, candidate.Id);
        Assert.Contains(keyword.Id, candidate.KeywordIds);
    }

    private static int _nextTmdbId = 90_000;

    private static int NextTmdbId() => Interlocked.Increment(ref _nextTmdbId);

    private static Genre CreateGenre(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"{name}-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };

    private static Keyword CreateKeyword() =>
        new()
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = NextTmdbId(),
            Name = $"keyword-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };

    private static Movie CreateMovie(int tmdbId)
    {
        var utcNow = DateTime.UtcNow;

        return new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Movie-{tmdbId}",
            OriginalTitle = $"Movie-{tmdbId}",
            Overview = "Overview",
            ReleaseDate = new DateOnly(2020, 1, 1),
            VoteAverage = 8m,
            VoteCount = 500,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static MovieGenre LinkMovieGenre(Movie movie, Genre genre) =>
        new()
        {
            MovieId = movie.Id,
            GenreId = genre.Id,
            Movie = movie,
            Genre = genre
        };
}
