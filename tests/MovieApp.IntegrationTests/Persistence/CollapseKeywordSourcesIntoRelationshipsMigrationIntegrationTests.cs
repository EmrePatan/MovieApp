using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Keywords;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Persistence;

public sealed class CollapseKeywordSourcesIntoRelationshipsMigrationIntegrationTests : IAsyncLifetime
{
    private const string IsolatedDatabaseName = "movieapp_collapse_keyword_sources_invariant";
    private const string MigrationBeforeCollapse = "20261007195000_AddWatchedEpisodesUserCoveringIndex";
    private const string CollapseMigration = "20261010155205_CollapseKeywordSourcesIntoRelationships";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task CollapseMigrationPreservesProviderMembershipAndCanReconstructLegacySourcesOnRollback()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync(MigrationBeforeCollapse);

        var now = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        var sharedKeywordId = Guid.NewGuid();
        var tvKeywordId = Guid.NewGuid();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO movies ("Id", "Title", "CreatedAt", "UpdatedAt", "VoteAverage", "VoteCount")
            VALUES ({movieId}, {"Collapse migration movie"}, {now}, {now}, {0m}, {0});

            INSERT INTO tv_shows ("Id", "Title", "CreatedAt", "UpdatedAt", "VoteAverage", "VoteCount", "Status")
            VALUES ({tvShowId}, {"Collapse migration tv"}, {now}, {now}, {0m}, {0}, {"Ended"});

            INSERT INTO keywords ("Id", "TmdbKeywordId", "Name", "CanonicalName", "NormalizedName", "CreatedAt", "UpdatedAt")
            VALUES
                ({sharedKeywordId}, {9_100_001}, {"shared source"}, {"shared source"}, {"shared source"}, {now}, {now}),
                ({tvKeywordId}, {9_100_002}, {"tv source"}, {"tv source"}, {"tv source"}, {now}, {now});

            INSERT INTO movie_keywords ("MovieId", "KeywordId")
            VALUES ({movieId}, {sharedKeywordId});

            INSERT INTO movie_keyword_sources ("MovieId", "KeywordId", "Provider", "FirstSeenAtUtc", "LastSeenAtUtc")
            VALUES
                ({movieId}, {sharedKeywordId}, {"Tmdb"}, {now.AddDays(-5)}, {now.AddDays(-1)}),
                ({movieId}, {sharedKeywordId}, {"MdbList"}, {now.AddDays(-4)}, {now});

            -- Deliberately omit the materialized TV relationship. The migration must recover it
            -- from the provenance row before the old source table is dropped.
            INSERT INTO tv_show_keyword_sources ("TvShowId", "KeywordId", "Provider", "FirstSeenAtUtc", "LastSeenAtUtc")
            VALUES ({tvShowId}, {tvKeywordId}, {"MdbList"}, {now.AddDays(-2)}, {now});
            """);

        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(CollapseMigration);
        context.ChangeTracker.Clear();

        var movieRelationship = await context.MovieKeywords
            .AsNoTracking()
            .SingleAsync(join => join.MovieId == movieId && join.KeywordId == sharedKeywordId);
        Assert.True(KeywordProviderSources.Contains(movieRelationship.Sources, KeywordProvider.Tmdb));
        Assert.True(KeywordProviderSources.Contains(movieRelationship.Sources, KeywordProvider.MdbList));

        var tvRelationship = await context.TvShowKeywords
            .AsNoTracking()
            .SingleAsync(join => join.TvShowId == tvShowId && join.KeywordId == tvKeywordId);
        Assert.False(KeywordProviderSources.Contains(tvRelationship.Sources, KeywordProvider.Tmdb));
        Assert.True(KeywordProviderSources.Contains(tvRelationship.Sources, KeywordProvider.MdbList));

        Assert.Null(await ScalarStringAsync(context, "SELECT to_regclass('public.movie_keyword_sources')::text"));
        Assert.Null(await ScalarStringAsync(context, "SELECT to_regclass('public.tv_show_keyword_sources')::text"));

        await migrator.MigrateAsync(MigrationBeforeCollapse);

        Assert.Equal(
            2,
            await ScalarCountAsync(
                context,
                $"SELECT COUNT(*) FROM movie_keyword_sources WHERE \"MovieId\" = '{movieId}' AND \"KeywordId\" = '{sharedKeywordId}'"));
        Assert.Equal(
            1,
            await ScalarCountAsync(
                context,
                $"SELECT COUNT(*) FROM tv_show_keyword_sources WHERE \"TvShowId\" = '{tvShowId}' AND \"KeywordId\" = '{tvKeywordId}'"));
        Assert.Equal(
            3,
            await ScalarCountAsync(
                context,
                "SELECT COUNT(*) FROM movie_keyword_sources WHERE \"FirstSeenAtUtc\" IS NULL AND \"LastSeenAtUtc\" IS NULL " +
                "UNION ALL SELECT COUNT(*) FROM tv_show_keyword_sources WHERE \"FirstSeenAtUtc\" IS NULL AND \"LastSeenAtUtc\" IS NULL"));
    }

    private static async Task<long> ScalarCountAsync(ApplicationDbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync();
        }

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ScalarStringAsync(ApplicationDbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync();
        }

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                IntegrationTestDatabase.GetConnectionString(IsolatedDatabaseName),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3))
            .Options;

        return new ApplicationDbContext(options);
    }
}
