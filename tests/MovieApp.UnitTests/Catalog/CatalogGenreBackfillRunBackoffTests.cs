using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.MovieChanges;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Catalog;

public sealed class CatalogGenreBackfillRunBackoffTests
{
    [Fact]
    public async Task RunAsyncDoesNotReselectProviderEmptyTopCandidateOnNextRun()
    {
        await using var context = CreateContext();
        var hotEmpty = await SeedMovieAsync(context, tmdbId: 5001, voteCount: 10_000);
        await SeedMovieAsync(context, tmdbId: 5002, voteCount: 100);
        var repository = new CatalogGenreBackfillRepository(context);
        var processCounts = new Dictionary<Guid, int>();
        var service = CreateService(context, repository, processCounts, addGenresOnRefresh: false);

        var firstRun = await service.RunAsync();
        var secondRun = await service.RunAsync();

        Assert.Equal(2, firstRun.Unrepairable);
        Assert.Equal(0, secondRun.Selected);
        Assert.Equal(1, processCounts.GetValueOrDefault(hotEmpty));
        Assert.Equal(2, processCounts.Count);
    }

    [Fact]
    public async Task SelectCandidatesAsyncPrefersNeverAttemptedGenreLessMovie()
    {
        await using var context = CreateContext();
        var neverAttempted = await SeedMovieAsync(context, tmdbId: 5101, voteCount: 1);
        var blocked = await SeedMovieAsync(context, tmdbId: 5102, voteCount: 99_999);
        context.CatalogGenreRepairAttempts.Add(new CatalogGenreRepairAttempt
        {
            Id = Guid.NewGuid(),
            CatalogId = blocked,
            ContentType = CatalogContentType.Movie,
            LastAttemptAtUtc = DateTime.UtcNow,
            LastOutcome = CatalogGenreRepairAttemptOutcome.ProviderNoUsableGenres,
            NextEligibleAtUtc = DateTime.UtcNow.AddDays(3)
        });
        await context.SaveChangesAsync();
        var repository = new CatalogGenreBackfillRepository(context);
        var service = CreateService(context, repository, new Dictionary<Guid, int>(), addGenresOnRefresh: true);

        var candidates = await service.SelectCandidatesAsync(1, []);

        Assert.Single(candidates);
        Assert.Equal(neverAttempted, candidates[0].CatalogId);
    }

    [Fact]
    public async Task SelectCandidatesAsyncDoesNotReturnMovieThatAlreadyHasGenres()
    {
        await using var context = CreateContext();
        await SeedMovieWithGenresAsync(context, tmdbId: 5201, voteCount: 50_000);
        var genreLess = await SeedMovieAsync(context, tmdbId: 5202, voteCount: 1);
        var repository = new CatalogGenreBackfillRepository(context);
        var service = CreateService(context, repository, new Dictionary<Guid, int>(), addGenresOnRefresh: true);

        var candidates = await service.SelectCandidatesAsync(5, []);

        Assert.Single(candidates);
        Assert.Equal(genreLess, candidates[0].CatalogId);
    }

    private static CatalogGenreBackfillService CreateService(
        ApplicationDbContext context,
        CatalogGenreBackfillRepository repository,
        Dictionary<Guid, int> processCounts,
        bool addGenresOnRefresh)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICatalogGenreBackfillItemProcessor>(_ =>
            new CountingProcessor(context, processCounts, addGenresOnRefresh));
        var provider = services.BuildServiceProvider();

        return new CatalogGenreBackfillService(
            repository,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CatalogGenreBackfillOptions
            {
                Enabled = true,
                BatchSize = 1,
                MaxConcurrency = 1,
                MaxBatchesPerRun = 2,
                DelayBetweenItemsMs = 0,
                DelayBetweenBatchesMs = 0,
                ProviderEmptyGenresRetryHours = 168,
                ProviderUnavailableRetryHours = 24,
                TransientFailureRetryMinutes = 15
            }));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"catalog-genre-run-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Guid> SeedMovieAsync(
        ApplicationDbContext context,
        int tmdbId,
        int voteCount)
    {
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = tmdbId,
            Title = $"Movie {tmdbId}",
            VoteCount = voteCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return movieId;
    }

    private static async Task SeedMovieWithGenresAsync(
        ApplicationDbContext context,
        int tmdbId,
        int voteCount)
    {
        var movieId = await SeedMovieAsync(context, tmdbId, voteCount);
        var genre = new Genre
        {
            Id = Guid.NewGuid(),
            Name = $"Genre-{movieId:N}",
            CreatedAt = DateTime.UtcNow
        };
        context.Genres.Add(genre);
        context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genre.Id });
        await context.SaveChangesAsync();
    }

    private sealed class CountingProcessor(
        ApplicationDbContext context,
        Dictionary<Guid, int> processCounts,
        bool addGenresOnRefresh) : ICatalogGenreBackfillItemProcessor
    {
        public async Task<CatalogGenreBackfillItemOutcome> ProcessAsync(
            CatalogGenreBackfillCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            processCounts[candidate.CatalogId] = processCounts.GetValueOrDefault(candidate.CatalogId) + 1;
            var repository = new CatalogGenreBackfillRepository(context);
            var inner = new CatalogGenreBackfillItemProcessor(
                new StubMovieRefreshService(context, addGenresOnRefresh),
                new StubTvShowRepository(context),
                new StubTvShowDataProvider(false),
                new StubCatalogProviderUpsertService(),
                repository,
                Options.Create(new CatalogGenreBackfillOptions()));

            return await inner.ProcessAsync(candidate, cancellationToken);
        }
    }

    private sealed class StubMovieRefreshService(ApplicationDbContext context, bool addGenresOnRefresh)
        : IMovieChangesTargetedRefreshService
    {
        public async Task<TmdbChangesTargetRefreshResult> RefreshRelevantMovieAsync(
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            if (addGenresOnRefresh)
            {
                var genre = new Genre
                {
                    Id = Guid.NewGuid(),
                    Name = $"Genre-{movieId:N}",
                    CreatedAt = DateTime.UtcNow
                };
                context.Genres.Add(genre);
                context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genre.Id });
                await context.SaveChangesAsync(cancellationToken);
            }

            return TmdbChangesTargetRefreshResult.Refreshed([]);
        }
    }

    private sealed class StubTvShowRepository(ApplicationDbContext context) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            context.TvShows.AsNoTracking().FirstOrDefaultAsync(tvShow => tvShow.Id == id, cancellationToken);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            context.TvShows.AsNoTracking().FirstOrDefaultAsync(tvShow => tvShow.TmdbId == tmdbId, cancellationToken);

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubTvShowDataProvider(bool returnsDetails) : ITvShowDataProvider
    {
        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(returnsDetails ? null : null);

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubCatalogProviderUpsertService : ICatalogProviderUpsertService
    {
        public Task<Movie> UpsertMovieFromProviderAsync(
            MovieProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertTvShowFromProviderAsync(
            TvShowProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
