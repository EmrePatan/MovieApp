using MovieApp.Application.Models.Recommendations;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Recommendations;

[Collection("CatalogPersistence")]
public sealed class SimilarCandidateIdBatchLoaderIntegrationTests
{
    [Fact]
    public async Task LoadMovieCandidateIdsAsyncReturnsEmptyDictionaryForEmptyInput()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);

        var results = await repository.GetSimilarMovieCandidateIdsForSourcesAsync([], maxCandidates: 5);

        Assert.Empty(results);
    }

    [Fact]
    public async Task LoadMovieCandidateIdsAsyncExcludesSourceAndOrdersByVoteCount()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);

        var actionGenre = CreateGenre("Action");
        var sourceMovie = CreateMovie(tmdbId: NextTmdbId(), voteCount: 50_000, voteAverage: 8.0m);
        var topCandidate = CreateMovie(tmdbId: NextTmdbId(), voteCount: 40_000, voteAverage: 7.0m);
        var secondCandidate = CreateMovie(tmdbId: NextTmdbId(), voteCount: 30_000, voteAverage: 9.5m);
        var unrelatedMovie = CreateMovie(tmdbId: NextTmdbId(), voteCount: 60_000, voteAverage: 9.0m);
        var dramaGenre = CreateGenre("Drama");

        context.Genres.AddRange(actionGenre, dramaGenre);
        context.Movies.AddRange(sourceMovie, topCandidate, secondCandidate, unrelatedMovie);
        context.MovieGenres.AddRange(
            LinkMovieGenre(sourceMovie, actionGenre),
            LinkMovieGenre(topCandidate, actionGenre),
            LinkMovieGenre(secondCandidate, actionGenre),
            LinkMovieGenre(unrelatedMovie, dramaGenre));
        await context.SaveChangesAsync();

        var batchResults = await repository.GetSimilarMovieCandidateIdsForSourcesAsync(
            [new SimilaritySourceGenreRequest(sourceMovie.Id, [actionGenre.Id])],
            maxCandidates: 5);
        var singleSourceResults = await repository.GetSimilarMovieCandidateIdsAsync(
            sourceMovie.Id,
            [actionGenre.Id],
            maxCandidates: 5);

        Assert.Equal([topCandidate.Id, secondCandidate.Id], batchResults[sourceMovie.Id]);
        Assert.Equal(batchResults[sourceMovie.Id], singleSourceResults);
        Assert.DoesNotContain(sourceMovie.Id, batchResults[sourceMovie.Id]);
        Assert.DoesNotContain(unrelatedMovie.Id, batchResults[sourceMovie.Id]);
    }

    [Fact]
    public async Task LoadTvShowCandidateIdsAsyncExcludesSourceAndOrdersByVoteCount()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);

        var sciFiGenre = CreateGenre("Sci-Fi");
        var sourceShow = CreateTvShow(tmdbId: NextTmdbId(), voteCount: 20_000, voteAverage: 8.0m);
        var topCandidate = CreateTvShow(tmdbId: NextTmdbId(), voteCount: 15_000, voteAverage: 7.5m);
        var secondCandidate = CreateTvShow(tmdbId: NextTmdbId(), voteCount: 10_000, voteAverage: 9.0m);

        context.Genres.Add(sciFiGenre);
        context.TvShows.AddRange(sourceShow, topCandidate, secondCandidate);
        context.TvShowGenres.AddRange(
            LinkTvShowGenre(sourceShow, sciFiGenre),
            LinkTvShowGenre(topCandidate, sciFiGenre),
            LinkTvShowGenre(secondCandidate, sciFiGenre));
        await context.SaveChangesAsync();

        var results = await repository.GetSimilarTvShowCandidateIdsForSourcesAsync(
            [new SimilaritySourceGenreRequest(sourceShow.Id, [sciFiGenre.Id])],
            maxCandidates: 5);

        Assert.Equal([topCandidate.Id, secondCandidate.Id], results[sourceShow.Id]);
        Assert.DoesNotContain(sourceShow.Id, results[sourceShow.Id]);
    }

    [Fact]
    public async Task LoadMovieCandidateIdsAsyncPartitionsResultsPerSourceInOneBatch()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);

        var actionGenre = CreateGenre("Action");
        var comedyGenre = CreateGenre("Comedy");

        var actionSource = CreateMovie(tmdbId: NextTmdbId(), voteCount: 90_000, voteAverage: 8.0m);
        var comedySource = CreateMovie(tmdbId: NextTmdbId(), voteCount: 80_000, voteAverage: 8.0m);
        var actionCandidate = CreateMovie(tmdbId: NextTmdbId(), voteCount: 70_000, voteAverage: 7.0m);
        var comedyCandidate = CreateMovie(tmdbId: NextTmdbId(), voteCount: 60_000, voteAverage: 7.0m);

        context.Genres.AddRange(actionGenre, comedyGenre);
        context.Movies.AddRange(actionSource, comedySource, actionCandidate, comedyCandidate);
        context.MovieGenres.AddRange(
            LinkMovieGenre(actionSource, actionGenre),
            LinkMovieGenre(comedySource, comedyGenre),
            LinkMovieGenre(actionCandidate, actionGenre),
            LinkMovieGenre(comedyCandidate, comedyGenre));
        await context.SaveChangesAsync();

        var results = await repository.GetSimilarMovieCandidateIdsForSourcesAsync(
            [
                new SimilaritySourceGenreRequest(actionSource.Id, [actionGenre.Id]),
                new SimilaritySourceGenreRequest(comedySource.Id, [comedyGenre.Id])
            ],
            maxCandidates: 3);

        Assert.Equal([actionCandidate.Id], results[actionSource.Id]);
        Assert.Equal([comedyCandidate.Id], results[comedySource.Id]);
    }

    [Fact]
    public async Task LoadMovieCandidateIdsAsyncRespectsMaxCandidatesPerSource()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);

        var genre = CreateGenre("Thriller");
        var sourceMovie = CreateMovie(tmdbId: NextTmdbId(), voteCount: 100_000, voteAverage: 8.0m);
        var candidates = Enumerable.Range(0, 4)
            .Select(index => CreateMovie(
                tmdbId: NextTmdbId(),
                voteCount: 90_000 - (index * 1_000),
                voteAverage: 7.0m))
            .ToList();

        context.Genres.Add(genre);
        context.Movies.Add(sourceMovie);
        context.Movies.AddRange(candidates);
        context.MovieGenres.Add(LinkMovieGenre(sourceMovie, genre));
        context.MovieGenres.AddRange(candidates.Select(candidate => LinkMovieGenre(candidate, genre)));
        await context.SaveChangesAsync();

        var results = await repository.GetSimilarMovieCandidateIdsForSourcesAsync(
            [new SimilaritySourceGenreRequest(sourceMovie.Id, [genre.Id])],
            maxCandidates: 2);

        Assert.Equal(2, results[sourceMovie.Id].Count);
        Assert.Equal(
            candidates.OrderByDescending(movie => movie.VoteCount).Take(2).Select(movie => movie.Id),
            results[sourceMovie.Id]);
    }

    [Fact]
    public async Task LoadMovieCandidateIdsAsyncSkipsSourcesWithoutGenres()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);

        var sourceMovie = CreateMovie(tmdbId: NextTmdbId(), voteCount: 10_000, voteAverage: 8.0m);
        context.Movies.Add(sourceMovie);
        await context.SaveChangesAsync();

        var results = await repository.GetSimilarMovieCandidateIdsForSourcesAsync(
            [new SimilaritySourceGenreRequest(sourceMovie.Id, [])],
            maxCandidates: 5);

        Assert.Empty(results);
    }

    private static int _nextTmdbId = 9_000_000;

    private static int NextTmdbId() => Interlocked.Increment(ref _nextTmdbId);

    private static Genre CreateGenre(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"{name}-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };

    private static Movie CreateMovie(int tmdbId, int voteCount, decimal voteAverage)
    {
        var utcNow = DateTime.UtcNow;

        return new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Movie-{tmdbId}",
            OriginalTitle = $"Movie-{tmdbId}",
            Overview = "Batch loader integration test movie.",
            ReleaseDate = new DateOnly(2020, 1, 1),
            RuntimeMinutes = 120,
            OriginalLanguage = "en",
            VoteAverage = voteAverage,
            VoteCount = voteCount,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static TvShow CreateTvShow(int tmdbId, int voteCount, decimal voteAverage)
    {
        var utcNow = DateTime.UtcNow;

        return new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Show-{tmdbId}",
            Overview = "Batch loader integration test show.",
            FirstAirDate = new DateOnly(2020, 1, 1),
            Status = TvShowStatus.ReturningSeries,
            VoteAverage = voteAverage,
            VoteCount = voteCount,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static MovieGenre LinkMovieGenre(Movie movie, Genre genre) =>
        new()
        {
            MovieId = movie.Id,
            Movie = movie,
            GenreId = genre.Id,
            Genre = genre
        };

    private static TvShowGenre LinkTvShowGenre(TvShow tvShow, Genre genre) =>
        new()
        {
            TvShowId = tvShow.Id,
            TvShow = tvShow,
            GenreId = genre.Id,
            Genre = genre
        };
}
