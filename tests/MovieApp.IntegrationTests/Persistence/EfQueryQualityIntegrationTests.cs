using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class EfQueryQualityIntegrationTests
{
    [Fact]
    public async Task SearchRepositoryDiscoveryPathsDoNotEmitRowLimitingWithoutOrderByWarning()
    {
        await using var context = CreateStrictQueryContext();
        await SeedDiscoveryCatalogAsync(context);

        var repository = new SearchRepository(context, Options.Create(new TopRatedOptions()), NullLogger<SearchRepository>.Instance);
        var criteria = new DiscoveryCriteria(SearchContentType.All, 1, 5);

        _ = await repository.GetTrendingAsync(criteria);
        _ = await repository.GetTopRatedAsync(criteria);
        _ = await repository.GetNewReleasesAsync(criteria);
        _ = await repository.SearchAsync(new SearchCriteria(
            "Trending",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            5));
    }

    [Fact]
    public async Task RecommendationRepositoryProjectionPathsDoNotEmitCollectionQueryWarnings()
    {
        await using var context = CreateStrictQueryContext();
        var (movieId, userId) = await SeedRecommendationCatalogAsync(context);

        var repository = new RecommendationRepository(context);
        _ = await repository.GetMovieSimilarityProfilesAsync([movieId]);
        _ = await repository.GetUserRecommendationContextAsync(userId);
    }

    [Fact]
    public async Task TvShowRepositoryLookupPathsDoNotEmitCollectionQueryWarnings()
    {
        await using var context = CreateStrictQueryContext();
        var (tvShowId, tmdbId) = await SeedTvShowCatalogAsync(context);

        var repository = new TvShowRepository(context);
        _ = await repository.GetByTmdbIdAsync(tmdbId);
        _ = await repository.GetByIdAsync(tvShowId);
    }

    [Fact]
    public async Task InsightsStatisticsAndLibraryPathsDoNotEmitFirstWithoutOrderByWarnings()
    {
        await using var context = CreateStrictQueryContext();
        var userId = await SeedUserAsync(context, $"ef-insights-{Guid.NewGuid():N}");

        var insightsRepository = CreateInsightsRepository(context);
        _ = await insightsRepository.GetSummaryRawDataAsync(userId);
        _ = await insightsRepository.GetAnalyticsRawDataAsync(userId, DateTime.UtcNow.AddDays(-30));
        _ = await insightsRepository.GetV3RawDataAsync(userId, TimeZoneInfo.Utc, DateTime.UtcNow.Year);

        _ = await new UserStatisticsRepository(context).GetStatisticsAsync(userId);

        var libraryRepository = new LibraryRepository(context);
        var libraryPage = new MovieApp.Application.Models.Library.LibraryPageRequest(1, 24, 25, null, true);
        _ = await libraryRepository.GetWatchingAsync(userId, SearchContentType.All, libraryPage);
        _ = await libraryRepository.GetWatchedAsync(userId, SearchContentType.All, libraryPage);
    }

    private static ApplicationDbContext CreateStrictQueryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString())
            .ConfigureWarnings(warnings => warnings
                .Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning)
                .Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning)
                .Throw(RelationalEventId.MultipleCollectionIncludeWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedDiscoveryCatalogAsync(ApplicationDbContext context)
    {
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        context.People.RemoveRange(context.People);

        var utcNow = DateTime.UtcNow;
        context.Movies.Add(new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Trending Movie",
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            VoteAverage = 8.5m,
            VoteCount = 500,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.TvShows.Add(new TvShow
        {
            Id = Guid.NewGuid(),
            Title = "Trending Show",
            FirstAirDate = DateOnly.FromDateTime(DateTime.UtcNow),
            VoteAverage = 8.0m,
            VoteCount = 400,
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.People.Add(new Person
        {
            Id = Guid.NewGuid(),
            Name = "Trending Person",
            TmdbId = 42,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedUserAsync(ApplicationDbContext context, string userName)
    {
        var utcNow = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"{userName}@example.com",
            NormalizedEmail = $"{userName}@example.com".ToUpperInvariant(),
            UserName = userName,
            DisplayName = userName,
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<(Guid MovieId, Guid UserId)> SeedRecommendationCatalogAsync(ApplicationDbContext context)
    {
        var utcNow = DateTime.UtcNow;
        var genreId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var keywordId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = userId,
            Email = "ef-query-quality@example.com",
            NormalizedEmail = "EF-QUERY-QUALITY@EXAMPLE.COM",
            UserName = "ef-query-quality",
            DisplayName = "EF Query Quality",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Genres.Add(new Genre
        {
            Id = genreId,
            Name = $"EF Query Quality {genreId:N}",
            CreatedAt = utcNow
        });
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            TmdbKeywordId = 101,
            Name = "hero",
            CreatedAt = utcNow
        });
        context.People.Add(new Person
        {
            Id = personId,
            Name = "Cast Member",
            TmdbId = 99,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Recommendation Seed",
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            VoteAverage = 8.0m,
            VoteCount = 100,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.MovieGenres.Add(new MovieGenre
        {
            MovieId = movieId,
            GenreId = genreId
        });
        context.MoviePeople.Add(new MoviePerson
        {
            MovieId = movieId,
            PersonId = personId,
            CreditType = CreditType.Cast
        });
        context.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movieId,
            KeywordId = keywordId
        });
        context.Ratings.Add(new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            Score = 9,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
        return (movieId, userId);
    }

    private static async Task<(Guid TvShowId, int TmdbId)> SeedTvShowCatalogAsync(ApplicationDbContext context)
    {
        var utcNow = DateTime.UtcNow;
        var genreId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        const int tmdbId = 4242;

        context.Genres.Add(new Genre
        {
            Id = genreId,
            Name = $"EF TV Query Quality {genreId:N}",
            CreatedAt = utcNow
        });
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = tmdbId,
            Title = "EF TV Query Quality Show",
            FirstAirDate = DateOnly.FromDateTime(DateTime.UtcNow),
            VoteAverage = 7.5m,
            VoteCount = 120,
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.TvShowGenres.Add(new TvShowGenre
        {
            TvShowId = tvShowId,
            GenreId = genreId
        });
        context.Seasons.Add(new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShowId,
            SeasonNumber = 1,
            Name = "Season 1",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
        return (tvShowId, tmdbId);
    }

    private static InsightsRepository CreateInsightsRepository(ApplicationDbContext context)
    {
        var connectionString = IntegrationTestDatabase.GetConnectionString();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new InsightsRepository(context, scopeFactory);
    }
}
