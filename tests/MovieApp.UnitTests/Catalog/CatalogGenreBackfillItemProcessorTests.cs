using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.MovieChanges;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Catalog;

public sealed class CatalogGenreBackfillItemProcessorTests
{
    [Fact]
    public async Task ProcessAsyncWhenMovieGainsGenresClearsRepairAttempt()
    {
        await using var context = CreateContext();
        var movieId = await SeedMovieAsync(context, tmdbId: 9001, voteCount: 10, withGenres: false);
        await SeedRepairAttemptAsync(
            context,
            movieId,
            CatalogContentType.Movie,
            CatalogGenreRepairAttemptOutcome.ProviderNoUsableGenres,
            DateTime.UtcNow.AddHours(1));
        var repository = new CatalogGenreBackfillRepository(context);
        var processor = CreateProcessor(
            context,
            repository,
            refreshOutcome: TmdbChangesTargetRefreshOutcome.Refreshed,
            addGenresOnRefresh: true);

        var outcome = await processor.ProcessAsync(
            new CatalogGenreBackfillCandidate(movieId, "movie", 9001));

        Assert.Equal(CatalogGenreBackfillItemOutcome.Succeeded, outcome);
        Assert.False(await context.CatalogGenreRepairAttempts.AnyAsync());
    }

    [Fact]
    public async Task ProcessAsyncWhenProviderReturnsEmptyGenresRecordsBackoff()
    {
        await using var context = CreateContext();
        var movieId = await SeedMovieAsync(context, tmdbId: 9002, voteCount: 10, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);
        var processor = CreateProcessor(
            context,
            repository,
            refreshOutcome: TmdbChangesTargetRefreshOutcome.Refreshed,
            addGenresOnRefresh: false);

        var before = DateTime.UtcNow;
        var outcome = await processor.ProcessAsync(
            new CatalogGenreBackfillCandidate(movieId, "movie", 9002));

        Assert.Equal(CatalogGenreBackfillItemOutcome.Unrepairable, outcome);
        var attempt = await context.CatalogGenreRepairAttempts.SingleAsync();
        Assert.Equal(CatalogGenreRepairAttemptOutcome.ProviderNoUsableGenres, attempt.LastOutcome);
        Assert.True(attempt.NextEligibleAtUtc >= before.AddHours(167));
    }

    [Fact]
    public async Task ProcessAsyncWhenProviderUnavailableRecordsBackoff()
    {
        await using var context = CreateContext();
        var movieId = await SeedMovieAsync(context, tmdbId: 9003, voteCount: 10, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);
        var processor = CreateProcessor(
            context,
            repository,
            refreshOutcome: TmdbChangesTargetRefreshOutcome.SkippedNotFound,
            addGenresOnRefresh: false);

        var outcome = await processor.ProcessAsync(
            new CatalogGenreBackfillCandidate(movieId, "movie", 9003));

        Assert.Equal(CatalogGenreBackfillItemOutcome.SkippedUnavailable, outcome);
        var attempt = await context.CatalogGenreRepairAttempts.SingleAsync();
        Assert.Equal(CatalogGenreRepairAttemptOutcome.ProviderUnavailable, attempt.LastOutcome);
    }

    [Fact]
    public async Task ProcessAsyncWhenTransientFailureRecordsShorterBackoff()
    {
        await using var context = CreateContext();
        var movieId = await SeedMovieAsync(context, tmdbId: 9004, voteCount: 10, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);
        var processor = CreateProcessor(
            context,
            repository,
            refreshOutcome: TmdbChangesTargetRefreshOutcome.Failed,
            addGenresOnRefresh: false);

        var before = DateTime.UtcNow;
        var outcome = await processor.ProcessAsync(
            new CatalogGenreBackfillCandidate(movieId, "movie", 9004));

        Assert.Equal(CatalogGenreBackfillItemOutcome.Failed, outcome);
        var attempt = await context.CatalogGenreRepairAttempts.SingleAsync();
        Assert.Equal(CatalogGenreRepairAttemptOutcome.FailedTransient, attempt.LastOutcome);
        Assert.True(attempt.NextEligibleAtUtc <= before.AddMinutes(16));
        Assert.True(attempt.NextEligibleAtUtc >= before.AddMinutes(14));
    }

    [Fact]
    public async Task ProcessAsyncWhenTvProviderEmptyGenresRecordsBackoff()
    {
        await using var context = CreateContext();
        var tvShowId = await SeedTvShowAsync(context, tmdbId: 9101, voteCount: 10, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);
        var processor = CreateProcessor(
            context,
            repository,
            refreshOutcome: TmdbChangesTargetRefreshOutcome.Refreshed,
            addGenresOnRefresh: false,
            tvProviderReturnsDetails: true,
            tvUpsertAddsGenres: false);

        var outcome = await processor.ProcessAsync(
            new CatalogGenreBackfillCandidate(tvShowId, "tv", 9101));

        Assert.Equal(CatalogGenreBackfillItemOutcome.Unrepairable, outcome);
        var attempt = await context.CatalogGenreRepairAttempts.SingleAsync();
        Assert.Equal(CatalogContentType.Tv, attempt.ContentType);
        Assert.Equal(CatalogGenreRepairAttemptOutcome.ProviderNoUsableGenres, attempt.LastOutcome);
    }

    [Fact]
    public async Task ProcessAsyncWhenTvProviderUnavailableRecordsBackoff()
    {
        await using var context = CreateContext();
        var tvShowId = await SeedTvShowAsync(context, tmdbId: 9102, voteCount: 10, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);
        var processor = CreateProcessor(
            context,
            repository,
            refreshOutcome: TmdbChangesTargetRefreshOutcome.Refreshed,
            addGenresOnRefresh: false,
            tvProviderReturnsDetails: false,
            tvUpsertAddsGenres: false);

        var outcome = await processor.ProcessAsync(
            new CatalogGenreBackfillCandidate(tvShowId, "tv", 9102));

        Assert.Equal(CatalogGenreBackfillItemOutcome.SkippedUnavailable, outcome);
        var attempt = await context.CatalogGenreRepairAttempts.SingleAsync();
        Assert.Equal(CatalogGenreRepairAttemptOutcome.ProviderUnavailable, attempt.LastOutcome);
    }

    [Fact]
    public async Task ProcessAsyncWhenProviderReturnedGenresButCatalogEmptyRecordsTransientFailure()
    {
        await using var context = CreateContext();
        var movieId = await SeedMovieAsync(context, tmdbId: 9005, voteCount: 10, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);
        var processor = CreateProcessor(
            context,
            repository,
            refreshOutcome: TmdbChangesTargetRefreshOutcome.Refreshed,
            addGenresOnRefresh: false,
            providerReturnedUsableGenres: true);

        var outcome = await processor.ProcessAsync(
            new CatalogGenreBackfillCandidate(movieId, "movie", 9005));

        Assert.Equal(CatalogGenreBackfillItemOutcome.Failed, outcome);
        var attempt = await context.CatalogGenreRepairAttempts.SingleAsync();
        Assert.Equal(CatalogGenreRepairAttemptOutcome.FailedTransient, attempt.LastOutcome);
    }

    [Fact]
    public async Task ProcessAsyncWhenTvProviderReturnedGenresButCatalogEmptyRecordsTransientFailure()
    {
        await using var context = CreateContext();
        var tvShowId = await SeedTvShowAsync(context, tmdbId: 9103, voteCount: 10, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);
        var processor = CreateProcessor(
            context,
            repository,
            refreshOutcome: TmdbChangesTargetRefreshOutcome.Refreshed,
            addGenresOnRefresh: false,
            tvProviderReturnsDetails: true,
            tvUpsertAddsGenres: false,
            tvProviderGenres: ["Horror"]);

        var outcome = await processor.ProcessAsync(
            new CatalogGenreBackfillCandidate(tvShowId, "tv", 9103));

        Assert.Equal(CatalogGenreBackfillItemOutcome.Failed, outcome);
        var attempt = await context.CatalogGenreRepairAttempts.SingleAsync();
        Assert.Equal(CatalogGenreRepairAttemptOutcome.FailedTransient, attempt.LastOutcome);
    }

    private static CatalogGenreBackfillItemProcessor CreateProcessor(
        ApplicationDbContext context,
        ICatalogGenreBackfillRepository repository,
        TmdbChangesTargetRefreshOutcome refreshOutcome,
        bool addGenresOnRefresh,
        bool providerReturnedUsableGenres = false,
        bool tvProviderReturnsDetails = true,
        bool tvUpsertAddsGenres = true,
        IReadOnlyList<string>? tvProviderGenres = null)
    {
        var movieRefresh = new StubMovieRefreshService(
            context,
            refreshOutcome,
            addGenresOnRefresh,
            providerReturnedUsableGenres);
        var tvShowRepository = new StubTvShowRepository(context);
        var tvProvider = new StubTvShowDataProvider(tvProviderReturnsDetails, tvProviderGenres ?? []);
        var upsertService = new StubCatalogProviderUpsertService(context, tvUpsertAddsGenres);

        return new CatalogGenreBackfillItemProcessor(
            movieRefresh,
            tvShowRepository,
            tvProvider,
            upsertService,
            repository,
            Options.Create(new CatalogGenreBackfillOptions
            {
                ProviderEmptyGenresRetryHours = 168,
                ProviderUnavailableRetryHours = 24,
                TransientFailureRetryMinutes = 15
            }));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"catalog-genre-processor-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Guid> SeedMovieAsync(
        ApplicationDbContext context,
        int tmdbId,
        int voteCount,
        bool withGenres)
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

        if (withGenres)
        {
            await AddMovieGenreAsync(context, movieId);
        }

        await context.SaveChangesAsync();
        return movieId;
    }

    private static async Task<Guid> SeedTvShowAsync(
        ApplicationDbContext context,
        int tmdbId,
        int voteCount,
        bool withGenres)
    {
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = tmdbId,
            Title = $"Tv {tmdbId}",
            VoteCount = voteCount,
            Status = TvShowStatus.Ended,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        if (withGenres)
        {
            await AddTvGenreAsync(context, tvShowId);
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task SeedRepairAttemptAsync(
        ApplicationDbContext context,
        Guid catalogId,
        CatalogContentType contentType,
        CatalogGenreRepairAttemptOutcome outcome,
        DateTime nextEligibleAtUtc)
    {
        context.CatalogGenreRepairAttempts.Add(new CatalogGenreRepairAttempt
        {
            Id = Guid.NewGuid(),
            CatalogId = catalogId,
            ContentType = contentType,
            LastAttemptAtUtc = DateTime.UtcNow.AddHours(-1),
            LastOutcome = outcome,
            NextEligibleAtUtc = nextEligibleAtUtc
        });

        await context.SaveChangesAsync();
    }

    private static async Task AddMovieGenreAsync(ApplicationDbContext context, Guid movieId)
    {
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

    private static async Task AddTvGenreAsync(ApplicationDbContext context, Guid tvShowId)
    {
        var genre = new Genre
        {
            Id = Guid.NewGuid(),
            Name = $"TvGenre-{tvShowId:N}",
            CreatedAt = DateTime.UtcNow
        };
        context.Genres.Add(genre);
        context.TvShowGenres.Add(new TvShowGenre { TvShowId = tvShowId, GenreId = genre.Id });
        await context.SaveChangesAsync();
    }

    private sealed class StubMovieRefreshService(
        ApplicationDbContext context,
        TmdbChangesTargetRefreshOutcome outcome,
        bool addGenresOnRefresh,
        bool providerReturnedUsableGenres) : IMovieChangesTargetedRefreshService
    {
        public async Task<TmdbChangesTargetRefreshResult> RefreshRelevantMovieAsync(
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            if (outcome == TmdbChangesTargetRefreshOutcome.Refreshed && addGenresOnRefresh)
            {
                await AddMovieGenreAsync(context, movieId);
            }

            return outcome switch
            {
                TmdbChangesTargetRefreshOutcome.Refreshed => TmdbChangesTargetRefreshResult.Refreshed(
                    [],
                    providerReturnedUsableGenres),
                TmdbChangesTargetRefreshOutcome.SkippedNotFound => TmdbChangesTargetRefreshResult.SkippedNotFound(),
                TmdbChangesTargetRefreshOutcome.SkippedUnavailable => TmdbChangesTargetRefreshResult.SkippedUnavailable(),
                TmdbChangesTargetRefreshOutcome.Failed => TmdbChangesTargetRefreshResult.Failed(),
                _ => TmdbChangesTargetRefreshResult.Failed()
            };
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

    private sealed class StubTvShowDataProvider(bool returnsDetails, IReadOnlyList<string> genres) : ITvShowDataProvider
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
            Task.FromResult<TvShowProviderDetails?>(
                returnsDetails
                    ? new TvShowProviderDetails(
                        ExternalId: externalId,
                        TmdbId: int.Parse(externalId, System.Globalization.CultureInfo.InvariantCulture),
                        TvdbId: null,
                        ImdbId: null,
                        Title: "Stub Tv",
                        OriginalTitle: "Stub Tv",
                        Overview: null,
                        FirstAirDate: null,
                        LastAirDate: null,
                        PosterPath: null,
                        BackdropPath: null,
                        OriginalLanguage: "en",
                        VoteAverage: 0,
                        VoteCount: 0,
                        Status: "Ended",
                        Genres: genres,
                        Seasons: [])
                    : null);

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

    private sealed class StubCatalogProviderUpsertService(
        ApplicationDbContext context,
        bool addGenres) : ICatalogProviderUpsertService
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

        public async Task<TvShow> UpsertTvShowFromProviderAsync(
            TvShowProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default)
        {
            if (addGenres)
            {
                var tvShow = await context.TvShows.FirstAsync(
                    item => item.TmdbId == details.TmdbId,
                    cancellationToken);
                await AddTvGenreAsync(context, tvShow.Id);
            }

            return await context.TvShows.FirstAsync(item => item.TmdbId == details.TmdbId, cancellationToken);
        }

        public Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
