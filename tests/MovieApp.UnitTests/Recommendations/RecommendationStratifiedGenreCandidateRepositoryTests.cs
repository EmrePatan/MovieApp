using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationStratifiedGenreCandidateRepositoryTests
{
    [Fact]
    public async Task GetPersonalizedCandidatesAsync_StratifiedGenres_UseBatchedBucketsEquivalentToSequentialPerGenre()
    {
        await using var context = CreateContext();
        var repository = new RecommendationRepository(
            context,
            recommendationOptions: Microsoft.Extensions.Options.Options.Create(new RecommendationOptions
            {
                CandidateMinPerGenre = 2,
                CandidateMinVoteCount = 0
            }));

        var genreA = CreateGenre("Action");
        var genreB = CreateGenre("Comedy");
        context.Genres.AddRange(genreA, genreB);

        var shared = CreateMovie("Shared Hit", voteCount: 5_000);
        var actionOnly = CreateMovie("Action Only", voteCount: 4_000);
        var comedyOnly = CreateMovie("Comedy Only", voteCount: 3_500);
        var filler = CreateMovie("Filler", voteCount: 100);
        context.Movies.AddRange(shared, actionOnly, comedyOnly, filler);
        context.MovieGenres.AddRange(
            Link(shared, genreA),
            Link(shared, genreB),
            Link(actionOnly, genreA),
            Link(comedyOnly, genreB),
            Link(filler, genreA));

        await context.SaveChangesAsync();

        var preferred = new[] { genreA.Id, genreB.Id };
        var actual = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.Movie,
            preferred,
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 3);

        var expectedIds = RecommendationCandidateBudget.RoundRobin(
            RecommendationStratifiedGenreSelection.BuildBuckets(
                RecommendationStratifiedGenreSelection.PrepareGenreOrder(preferred),
                new[]
                {
                    new StratifiedGenreVoteRow(genreA.Id, shared.Id, shared.VoteCount, shared.VoteAverage),
                    new StratifiedGenreVoteRow(genreA.Id, actionOnly.Id, actionOnly.VoteCount, actionOnly.VoteAverage),
                    new StratifiedGenreVoteRow(genreA.Id, filler.Id, filler.VoteCount, filler.VoteAverage),
                    new StratifiedGenreVoteRow(genreB.Id, shared.Id, shared.VoteCount, shared.VoteAverage),
                    new StratifiedGenreVoteRow(genreB.Id, comedyOnly.Id, comedyOnly.VoteCount, comedyOnly.VoteAverage)
                },
                perGenreLimit: 2),
            maxCandidates: 3);

        Assert.Equal(expectedIds, actual.Select(candidate => candidate.Id).ToList());
    }

    [Fact]
    public async Task GetPersonalizedCandidatesAsync_SinglePreferredGenre_SkipsStratifiedBatching()
    {
        await using var context = CreateContext();
        var repository = new RecommendationRepository(context);
        var genre = CreateGenre("Drama");
        var movie = CreateMovie("Only", voteCount: 500);
        context.Genres.Add(genre);
        context.Movies.Add(movie);
        context.MovieGenres.Add(Link(movie, genre));
        await context.SaveChangesAsync();

        var candidates = await repository.GetPersonalizedCandidatesAsync(
            RecommendationContentType.Movie,
            [genre.Id],
            excludedMovieIds: new HashSet<Guid>(),
            excludedTvShowIds: new HashSet<Guid>(),
            maxCandidates: 500);

        Assert.Single(candidates);
        Assert.Equal(movie.Id, candidates[0].Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"stratified-genre-candidates-{Guid.NewGuid()}")
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

    private static Movie CreateMovie(string title, int voteCount) =>
        new()
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_000_000),
            Title = title,
            ReleaseDate = new DateOnly(2020, 1, 1),
            VoteCount = voteCount,
            VoteAverage = 7.5m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static MovieGenre Link(Movie movie, Genre genre) =>
        new()
        {
            MovieId = movie.Id,
            GenreId = genre.Id,
            Movie = movie,
            Genre = genre
        };
}
