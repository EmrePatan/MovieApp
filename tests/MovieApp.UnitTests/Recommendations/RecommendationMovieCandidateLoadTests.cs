using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Configuration;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationMovieCandidateLoadTests
{
    [Fact]
    public async Task GetPersonalizedCandidatesAsync_Movie_ReturnsSameScoredWinner_WithTwoPhaseMovieLoad()
    {
        await using var context = CreateContext();
        var repository = new RecommendationRepository(context);
        var genre = CreateGenre("Action");
        var keyword = CreateKeyword();
        var highVoteMovie = CreateMovie("Alpha Hit", voteCount: 10_000, voteAverage: 8.5m);
        var lowVoteMovie = CreateMovie("Beta Niche", voteCount: 100, voteAverage: 9.0m);

        context.Genres.Add(genre);
        context.Keywords.Add(keyword);
        context.Movies.AddRange(highVoteMovie, lowVoteMovie);
        context.MovieGenres.AddRange(
            LinkMovieGenre(highVoteMovie, genre),
            LinkMovieGenre(lowVoteMovie, genre));
        context.MovieKeywords.AddRange(
            new MovieKeyword { MovieId = highVoteMovie.Id, KeywordId = keyword.Id },
            new MovieKeyword { MovieId = lowVoteMovie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.Movie,
            [genre.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 10);

        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, candidate => Assert.Contains(keyword.Id, candidate.KeywordIds));
        Assert.All(candidates, candidate => Assert.Empty(candidate.PersonIds));

        var options = new RecommendationOptions();
        var utcNow = DateTime.UtcNow;
        var signals = new List<UserBehaviorSignal>
        {
            new(
                highVoteMovie.Id,
                "movie",
                UserBehaviorSignalTypes.Favorite,
                highVoteMovie.Title,
                null,
                utcNow,
                [genre.Id],
                new Dictionary<Guid, string> { [genre.Id] = genre.Name },
                [])
            {
                KeywordIds = [keyword.Id],
                CatalogVoteAverage = highVoteMovie.VoteAverage,
                CatalogYear = highVoteMovie.ReleaseDate?.Year
            }
        };

        var genrePreferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, options, utcNow);
        var keywordPreferences = KeywordAffinityScorer.BuildKeywordPreferences(signals, options, utcNow);
        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            candidates,
            signals,
            genrePreferences,
            keywordPreferences,
            options,
            utcNow);

        Assert.Equal(highVoteMovie.Id, scored[0].Candidate.Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"recommendation-movie-candidates-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Genre CreateGenre(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

    private static Keyword CreateKeyword() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"keyword-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };

    private static Movie CreateMovie(string title, int voteCount, decimal voteAverage) =>
        new()
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_000_000),
            Title = title,
            ReleaseDate = new DateOnly(2020, 1, 1),
            VoteCount = voteCount,
            VoteAverage = voteAverage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static MovieGenre LinkMovieGenre(Movie movie, Genre genre) =>
        new()
        {
            MovieId = movie.Id,
            GenreId = genre.Id,
            Movie = movie,
            Genre = genre
        };
}
