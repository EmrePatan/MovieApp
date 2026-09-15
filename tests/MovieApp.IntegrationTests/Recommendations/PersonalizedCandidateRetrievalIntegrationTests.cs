using MovieApp.Application.Models.Recommendations;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Recommendations;

[Collection("CatalogPersistence")]
public sealed class PersonalizedCandidateRetrievalIntegrationTests
{
    [Fact]
    public async Task PersonalizedCandidatesExcludeFutureMoviesAndTvShows()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);
        var genre = CreateGenre("Sci-Fi");
        var releasedMovie = CreateMovie(tmdbId: NextTmdbId(), releaseDate: new DateOnly(2020, 1, 1));
        var futureMovie = CreateMovie(tmdbId: NextTmdbId(), releaseDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));
        var releasedTv = CreateTvShow(tmdbId: NextTmdbId(), firstAirDate: new DateOnly(2020, 1, 1));
        var futureTv = CreateTvShow(tmdbId: NextTmdbId(), firstAirDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        context.Genres.Add(genre);
        context.Movies.AddRange(releasedMovie, futureMovie);
        context.TvShows.AddRange(releasedTv, futureTv);
        context.MovieGenres.AddRange(
            LinkMovieGenre(releasedMovie, genre),
            LinkMovieGenre(futureMovie, genre));
        context.TvShowGenres.AddRange(
            LinkTvShowGenre(releasedTv, genre),
            LinkTvShowGenre(futureTv, genre));
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.All,
            [genre.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 50);

        Assert.Contains(candidates, candidate => candidate.Id == releasedMovie.Id);
        Assert.Contains(candidates, candidate => candidate.Id == releasedTv.Id);
        Assert.DoesNotContain(candidates, candidate => candidate.Id == futureMovie.Id);
        Assert.DoesNotContain(candidates, candidate => candidate.Id == futureTv.Id);
    }

    [Fact]
    public async Task PersonalizedCandidatesKeepNullReleaseDatesEligible()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);
        var genre = CreateGenre("Drama");
        var unknownDateMovie = CreateMovie(tmdbId: NextTmdbId(), releaseDate: null);

        context.Genres.Add(genre);
        context.Movies.Add(unknownDateMovie);
        context.MovieGenres.Add(LinkMovieGenre(unknownDateMovie, genre));
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.Movie,
            [genre.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 10);

        Assert.Contains(candidates, candidate => candidate.Id == unknownDateMovie.Id);
    }

    private static int _nextTmdbId = 80_000;

    private static int NextTmdbId() => Interlocked.Increment(ref _nextTmdbId);

    private static Genre CreateGenre(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"{name}-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };

    private static Movie CreateMovie(int tmdbId, DateOnly? releaseDate)
    {
        var utcNow = DateTime.UtcNow;

        return new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Movie-{tmdbId}",
            OriginalTitle = $"Movie-{tmdbId}",
            Overview = "Overview",
            ReleaseDate = releaseDate,
            VoteAverage = 7m,
            VoteCount = 100,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static TvShow CreateTvShow(int tmdbId, DateOnly? firstAirDate)
    {
        var utcNow = DateTime.UtcNow;

        return new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Show-{tmdbId}",
            Overview = "Overview",
            FirstAirDate = firstAirDate,
            Status = TvShowStatus.ReturningSeries,
            VoteAverage = 7m,
            VoteCount = 100,
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

    private static TvShowGenre LinkTvShowGenre(TvShow tvShow, Genre genre) =>
        new()
        {
            TvShowId = tvShow.Id,
            GenreId = genre.Id,
            TvShow = tvShow,
            Genre = genre
        };
}
