using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Persistence;

public sealed class ExpandKeywordGraphPr1MigrationInvariantIntegrationTests : IAsyncLifetime
{
    private const string IsolatedDatabaseName = "movieapp_expand_keyword_graph_pr1_invariant";
    private const string MigrationBeforeExpand = "20260930062626_AddUserAvatarFields";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task ExpandKeywordGraphPr1BackfillsExternalReferencesAndPreservesJoinTables()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync(MigrationBeforeExpand);

        var now = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        var keywordTimeTravelId = Guid.NewGuid();
        var keywordTimeTravelHyphenId = Guid.NewGuid();
        var keywordSciFiId = Guid.NewGuid();
        const int tmdbTimeTravel = 9_001_001;
        const int tmdbTimeTravelHyphen = 9_001_002;
        const int tmdbSciFi = 9_001_003;

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO movies ("Id", "Title", "CreatedAt", "UpdatedAt", "VoteAverage", "VoteCount")
             VALUES ({movieId}, {"Migration invariant movie"}, {now}, {now}, {0m}, {0});
             INSERT INTO tv_shows ("Id", "Title", "CreatedAt", "UpdatedAt", "VoteAverage", "VoteCount", "Status")
             VALUES ({tvShowId}, {"Migration invariant tv"}, {now}, {now}, {0m}, {0}, {"Ended"});
             INSERT INTO keywords ("Id", "TmdbKeywordId", "Name", "CreatedAt", "UpdatedAt")
             VALUES
               ({keywordTimeTravelId}, {tmdbTimeTravel}, {"Time Travel"}, {now}, {now}),
               ({keywordTimeTravelHyphenId}, {tmdbTimeTravelHyphen}, {"time-travel"}, {now}, {now}),
               ({keywordSciFiId}, {tmdbSciFi}, {"SCI_FI"}, {now}, {now});
             INSERT INTO movie_keywords ("MovieId", "KeywordId")
             VALUES ({movieId}, {keywordTimeTravelId}), ({movieId}, {keywordTimeTravelHyphenId});
             INSERT INTO tv_show_keywords ("TvShowId", "KeywordId")
             VALUES ({tvShowId}, {keywordSciFiId});
             """);

        var keywordCountBefore = await ScalarCountAsync(context, "SELECT COUNT(*) FROM keywords");
        var movieKeywordCountBefore = await ScalarCountAsync(context, "SELECT COUNT(*) FROM movie_keywords");
        var tvShowKeywordCountBefore = await ScalarCountAsync(context, "SELECT COUNT(*) FROM tv_show_keywords");

        Assert.Equal(3, keywordCountBefore);
        Assert.Equal(2, movieKeywordCountBefore);
        Assert.Equal(1, tvShowKeywordCountBefore);

        var migrator = context.Database.GetService<IMigrator>();
        migrator.Migrate("20260930100106_ExpandKeywordGraphPr1");

        var keywordCountAfter = await context.Keywords.CountAsync();
        var tmdbRefCount = await context.KeywordExternalReferences
            .CountAsync(reference => reference.Provider == KeywordProvider.Tmdb);
        var movieKeywordCountAfter = await context.MovieKeywords.CountAsync();
        var tvShowKeywordCountAfter = await context.TvShowKeywords.CountAsync();
        var movieSourceCount = await context.MovieKeywordSources.CountAsync();
        var tvSourceCount = await context.TvShowKeywordSources.CountAsync();

        Assert.Equal(keywordCountBefore, keywordCountAfter);
        Assert.Equal(keywordCountAfter, tmdbRefCount);
        Assert.Equal(movieKeywordCountBefore, movieKeywordCountAfter);
        Assert.Equal(tvShowKeywordCountBefore, tvShowKeywordCountAfter);
        Assert.Equal(0, movieSourceCount);
        Assert.Equal(0, tvSourceCount);

        var keywords = await context.Keywords.AsNoTracking().OrderBy(keyword => keyword.TmdbKeywordId).ToListAsync();
        Assert.Equal(3, keywords.Count);
        Assert.Equal("time travel", keywords[0].NormalizedName);
        Assert.Equal("time travel", keywords[1].NormalizedName);
        Assert.Equal("sci fi", keywords[2].NormalizedName);

        foreach (var keyword in keywords)
        {
            Assert.Equal(keyword.Name, keyword.CanonicalName);
            var reference = await context.KeywordExternalReferences
                .AsNoTracking()
                .SingleAsync(
                    item => item.KeywordId == keyword.Id && item.Provider == KeywordProvider.Tmdb);
            Assert.Equal(keyword.TmdbKeywordId.ToString(CultureInfo.InvariantCulture), reference.ExternalId);
            Assert.Equal(keyword.Name, reference.ExternalName);
        }
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
