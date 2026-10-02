using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Keywords;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Recommendations;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Keywords;

[Collection("CatalogPersistence")]
public sealed class KeywordDisplayProfileRefreshServiceIntegrationTests
{
    [Fact]
    public async Task RefreshAsyncCompletesUnderNpgsqlRetryExecutionStrategy()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var keywordId = await SeedEligibleKeywordMovieGraphAsync(context);

        var service = CreateService();
        var result = await service.RefreshAsync();

        Assert.True(result.Succeeded);
        Assert.True(result.ProfilesWritten > 0);
        var stored = await context.KeywordDisplayProfiles
            .AsNoTracking()
            .SingleAsync(profile => profile.KeywordId == keywordId);
        Assert.True(stored.DocumentFrequency > 0);
    }

    private static KeywordDisplayProfileRefreshService CreateService()
    {
        var factory = CreateDbContextFactory();
        var loader = new KeywordDisplayProfileLoader(
            factory,
            Options.Create(new KeywordCatalogStatisticsOptions()));

        return new KeywordDisplayProfileRefreshService(
            factory,
            loader,
            Options.Create(new KeywordDisplayProfileOptions
            {
                Enabled = true,
                MinimumDocumentFrequency = 1,
            }),
            NullLogger<KeywordDisplayProfileRefreshService>.Instance);
    }

    private static IDbContextFactory<ApplicationDbContext> CreateDbContextFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(
                IntegrationTestDatabase.GetConnectionString(),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

        return services
            .BuildServiceProvider()
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    }

    private static async Task<Guid> SeedEligibleKeywordMovieGraphAsync(ApplicationDbContext context)
    {
        var utcNow = DateTime.UtcNow;
        var keywordId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var genreId = Guid.NewGuid();

        context.Genres.Add(new Genre
        {
            Id = genreId,
            Name = "Integration Genre",
            CreatedAt = utcNow,
        });
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            TmdbKeywordId = Random.Shared.Next(1_000_000, 9_999_999),
            Name = "integration keyword",
            CanonicalName = "integration keyword",
            NormalizedName = "integration keyword",
            SemanticCategory = KeywordSemanticCategory.Unknown,
            ClassificationStatus = KeywordClassificationStatus.Auto,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = Random.Shared.Next(10_000_000, 99_999_999),
            Title = "Keyword profile movie",
            VoteCount = 100,
            VoteAverage = 7.5m,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.MovieGenres.Add(new MovieGenre
        {
            MovieId = movieId,
            GenreId = genreId,
        });
        context.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movieId,
            KeywordId = keywordId,
        });
        await context.SaveChangesAsync();
        return keywordId;
    }
}
