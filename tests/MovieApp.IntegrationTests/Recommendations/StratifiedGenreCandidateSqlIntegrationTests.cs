using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.Persistence;
using Microsoft.Extensions.Options;

namespace MovieApp.IntegrationTests.Recommendations;

[Collection("CatalogPersistence")]
public sealed class StratifiedGenreCandidateSqlIntegrationTests
{
    [Fact]
    public async Task PersonalizedMovieCandidates_StratifiedPreferredGenres_UseSingleBatchedGenreQuery()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new RecommendationRepository(
            context,
            recommendationOptions: Options.Create(new RecommendationOptions
            {
                CandidateMinPerGenre = 2,
                CandidateMinVoteCount = 0
            }));

        var genreA = CreateGenre("Action");
        var genreB = CreateGenre("Comedy");
        context.Genres.AddRange(genreA, genreB);

        var shared = CreateMovie(tmdbId: NextTmdbId(), voteCount: 5_000);
        var actionOnly = CreateMovie(tmdbId: NextTmdbId(), voteCount: 4_000);
        var comedyOnly = CreateMovie(tmdbId: NextTmdbId(), voteCount: 3_500);
        context.Movies.AddRange(shared, actionOnly, comedyOnly);
        context.MovieGenres.AddRange(
            LinkMovieGenre(shared, genreA),
            LinkMovieGenre(shared, genreB),
            LinkMovieGenre(actionOnly, genreA),
            LinkMovieGenre(comedyOnly, genreB));
        await context.SaveChangesAsync();

        var preferred = new[] { genreA.Id, genreB.Id };
        var candidates = await repository.GetPersonalizedCandidatesAsync(
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
                    new StratifiedGenreVoteRow(genreB.Id, shared.Id, shared.VoteCount, shared.VoteAverage),
                    new StratifiedGenreVoteRow(genreB.Id, comedyOnly.Id, comedyOnly.VoteCount, comedyOnly.VoteAverage)
                },
                perGenreLimit: 2),
            maxCandidates: 3);

        Assert.Equal(expectedIds, candidates.Select(candidate => candidate.Id).ToList());
    }

    private static int _tmdbSeed = 40_000_000;

    private static int NextTmdbId() => Interlocked.Increment(ref _tmdbSeed);

    private static Genre CreateGenre(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

    private static Movie CreateMovie(int tmdbId, int voteCount) =>
        new()
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Movie-{tmdbId}",
            ReleaseDate = new DateOnly(2020, 1, 1),
            VoteCount = voteCount,
            VoteAverage = 7.5m,
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
