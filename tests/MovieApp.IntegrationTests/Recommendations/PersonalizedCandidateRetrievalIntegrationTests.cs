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

    [Fact]
    public async Task PersonalizedCandidatesKeepTvWhenMovieCatalogFillsTheOldBudget()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);
        var genre = CreateGenre("Action");
        context.Genres.Add(genre);

        var movies = Enumerable.Range(0, 500)
            .Select(index => CreateMovie(tmdbId: NextTmdbId(), releaseDate: new DateOnly(2018, 1, 1), voteCount: 5_000 - index))
            .ToList();
        var shows = Enumerable.Range(0, 3)
            .Select(index => CreateTvShow(tmdbId: NextTmdbId(), firstAirDate: new DateOnly(2019, 1, 1), voteCount: 80 + index))
            .ToList();

        context.Movies.AddRange(movies);
        context.TvShows.AddRange(shows);
        context.MovieGenres.AddRange(movies.Select(movie => LinkMovieGenre(movie, genre)));
        context.TvShowGenres.AddRange(shows.Select(show => LinkTvShowGenre(show, genre)));
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.All,
            [genre.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 500);

        Assert.Equal(500, candidates.Count);
        Assert.All(shows, show => Assert.Contains(candidates, candidate => candidate.Id == show.Id && candidate.Type == "tv"));
        Assert.Contains(candidates, candidate => candidate.Type == "movie");
    }

    [Fact]
    public async Task PersonalizedTvCandidatesFillTheRequestedSection()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);
        var genre = CreateGenre("Drama");
        context.Genres.Add(genre);

        var movies = Enumerable.Range(0, 30)
            .Select(_ => CreateMovie(tmdbId: NextTmdbId(), releaseDate: new DateOnly(2018, 1, 1), voteCount: 9_000))
            .ToList();
        var shows = Enumerable.Range(0, 12)
            .Select(index => CreateTvShow(tmdbId: NextTmdbId(), firstAirDate: new DateOnly(2019, 1, 1), voteCount: 400 - index))
            .ToList();

        context.Movies.AddRange(movies);
        context.TvShows.AddRange(shows);
        context.MovieGenres.AddRange(movies.Select(movie => LinkMovieGenre(movie, genre)));
        context.TvShowGenres.AddRange(shows.Select(show => LinkTvShowGenre(show, genre)));
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.Tv,
            [genre.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 10);

        Assert.Equal(10, candidates.Count);
        Assert.All(candidates, candidate => Assert.Equal("tv", candidate.Type));
    }

    [Fact]
    public async Task PersonalizedCandidatesReserveSlotsForALowerVoteGenre()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);
        var action = CreateGenre("Action");
        var drama = CreateGenre("Drama");
        context.Genres.AddRange(action, drama);

        var actionMovies = Enumerable.Range(0, 25)
            .Select(index => CreateMovie(tmdbId: NextTmdbId(), releaseDate: new DateOnly(2018, 1, 1), voteCount: 8_000 - index))
            .ToList();
        var dramaMovies = Enumerable.Range(0, 4)
            .Select(index => CreateMovie(tmdbId: NextTmdbId(), releaseDate: new DateOnly(2017, 1, 1), voteCount: 40 + index))
            .ToList();

        context.Movies.AddRange(actionMovies);
        context.Movies.AddRange(dramaMovies);
        context.MovieGenres.AddRange(actionMovies.Select(movie => LinkMovieGenre(movie, action)));
        context.MovieGenres.AddRange(dramaMovies.Select(movie => LinkMovieGenre(movie, drama)));
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.Movie,
            [action.Id, drama.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 10);

        Assert.Equal(10, candidates.Count);
        Assert.Contains(candidates, candidate => dramaMovies.Any(movie => movie.Id == candidate.Id));
        Assert.Contains(candidates, candidate => actionMovies.Any(movie => movie.Id == candidate.Id));
    }

    [Fact]
    public async Task PersonalizedCandidatesMarkConfiguredFranchiseKeywords()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(context);
        var genre = CreateGenre("Adventure");
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = NextTmdbId(),
            Name = "Marvel Cinematic Universe",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var movie = CreateMovie(tmdbId: NextTmdbId(), releaseDate: new DateOnly(2012, 5, 4), voteCount: 10_000);

        context.Genres.Add(genre);
        context.Keywords.Add(keyword);
        context.Movies.Add(movie);
        context.MovieGenres.Add(LinkMovieGenre(movie, genre));
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.Movie,
            [genre.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 5);

        var match = Assert.Single(candidates, candidate => candidate.Id == movie.Id);
        Assert.Contains(keyword.Id, match.KeywordIds);
        Assert.Contains(keyword.Id, match.FranchiseKeywordIds);
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

    private static Movie CreateMovie(int tmdbId, DateOnly? releaseDate, int voteCount = 100)
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
            VoteCount = voteCount,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static TvShow CreateTvShow(int tmdbId, DateOnly? firstAirDate, int voteCount = 100)
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
            VoteCount = voteCount,
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
