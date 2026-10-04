using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Keywords;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Recommendations;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Keywords;

[Collection("CatalogPersistence")]
public sealed class KeywordClassificationGovernanceIntegrationTests
{
    [Fact]
    public async Task DisplayProfileRefreshOmitsExcludedKeywordFromDocumentFrequency()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var utcNow = DateTime.UtcNow;
        var genreId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var usableKeywordId = Guid.NewGuid();
        var excludedKeywordId = Guid.NewGuid();

        context.Genres.Add(new Genre { Id = genreId, Name = "Drama", CreatedAt = utcNow });
        context.Keywords.AddRange(
            new Keyword
            {
                Id = usableKeywordId,
                TmdbKeywordId = Random.Shared.Next(1_000_000, 1_999_999),
                Name = "usable keyword",
                CanonicalName = "usable keyword",
                NormalizedName = "usable keyword",
                ClassificationStatus = KeywordClassificationStatus.Approved,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            },
            new Keyword
            {
                Id = excludedKeywordId,
                TmdbKeywordId = Random.Shared.Next(2_000_000, 2_999_999),
                Name = "excluded spam",
                CanonicalName = "excluded spam",
                NormalizedName = "excluded spam",
                ClassificationStatus = KeywordClassificationStatus.Excluded,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = Random.Shared.Next(10_000_000, 99_999_999),
            Title = "Governance movie",
            VoteCount = 200,
            VoteAverage = 8m,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
        context.MovieKeywords.AddRange(
            new MovieKeyword { MovieId = movieId, KeywordId = usableKeywordId },
            new MovieKeyword { MovieId = movieId, KeywordId = excludedKeywordId });
        await context.SaveChangesAsync();

        var factory = CreateDbContextFactory();
        var service = new KeywordDisplayProfileRefreshService(
            factory,
            new KeywordDisplayProfileLoader(factory, Options.Create(new KeywordCatalogStatisticsOptions())),
            Options.Create(new KeywordDisplayProfileOptions
            {
                Enabled = true,
                MinimumDocumentFrequency = 1,
            }),
            NullLogger<KeywordDisplayProfileRefreshService>.Instance);

        var result = await service.RefreshAsync();
        Assert.True(result.Succeeded);

        await using var verify = CatalogPersistenceFixture.CreateContext();
        var usableProfile = await verify.KeywordDisplayProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.KeywordId == usableKeywordId);
        var excludedProfile = await verify.KeywordDisplayProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.KeywordId == excludedKeywordId);

        Assert.NotNull(usableProfile);
        Assert.True(usableProfile!.DocumentFrequency > 0);
        Assert.Null(excludedProfile);
    }

    [Fact]
    public async Task KeywordCatalogRepositorySyncPreservesApprovedAndExcludedClassification()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var utcNow = DateTime.UtcNow;
        const int tmdbKeywordId = 8_888_001;
        var keywordId = Guid.NewGuid();
        var movieId = Guid.NewGuid();

        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            TmdbKeywordId = tmdbKeywordId,
            Name = "before sync",
            CanonicalName = "before sync",
            NormalizedName = "before sync",
            ClassificationStatus = KeywordClassificationStatus.Excluded,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = Random.Shared.Next(10_000_000, 99_999_999),
            Title = "Sync movie",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var repository = new MovieApp.Infrastructure.Persistence.Repositories.KeywordCatalogRepository(
            context,
            Options.Create(new KeywordGraphOptions()));

        await repository.SyncMovieKeywordsAsync(
            movieId,
            [new ProviderKeywordSummary(tmdbKeywordId, "after sync")],
            utcNow.AddMinutes(1));

        await context.Entry(context.Keywords.Single(keyword => keyword.Id == keywordId)).ReloadAsync();
        var keyword = await context.Keywords.AsNoTracking().SingleAsync(k => k.Id == keywordId);

        Assert.Equal(KeywordClassificationStatus.Excluded, keyword.ClassificationStatus);
        Assert.Equal("after sync", keyword.Name);
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
}
