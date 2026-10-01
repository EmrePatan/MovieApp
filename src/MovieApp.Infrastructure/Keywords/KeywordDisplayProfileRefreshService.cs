using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Recommendations;

namespace MovieApp.Infrastructure.Keywords;

public sealed class KeywordDisplayProfileRefreshService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    KeywordDisplayProfileLoader loader,
    IOptions<KeywordDisplayProfileOptions> options,
    ILogger<KeywordDisplayProfileRefreshService> logger) : IKeywordDisplayProfileRefreshService
{
    public async Task<KeywordDisplayProfileRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return new KeywordDisplayProfileRefreshResult(
                Succeeded: true,
                ProfilesWritten: 0,
                DisplayableCount: 0,
                DurationMilliseconds: 0,
                FailureReason: "disabled");
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var loadResult = await loader.LoadAsync(cancellationToken);
            if (loadResult is null)
            {
                return new KeywordDisplayProfileRefreshResult(
                    Succeeded: false,
                    ProfilesWritten: 0,
                    DisplayableCount: 0,
                    DurationMilliseconds: stopwatch.ElapsedMilliseconds,
                    FailureReason: "empty-load-result");
            }

            var now = DateTime.UtcNow;
            var profiles = new List<KeywordDisplayProfile>(loadResult.Rows.Count);
            var displayableCount = 0;

            foreach (var row in loadResult.Rows)
            {
                if (!loadResult.CanonicalNamesByKeywordId.TryGetValue(row.KeywordId, out var canonicalName))
                {
                    continue;
                }

                var quality = KeywordDisplayQualityEvaluator.Evaluate(
                    canonicalName,
                    row.DocumentFrequency,
                    loadResult.CatalogDocumentCount,
                    row.MovieTitleCount,
                    row.TvTitleCount,
                    options.Value);

                if (quality.Displayable)
                {
                    displayableCount++;
                }

                profiles.Add(new KeywordDisplayProfile
                {
                    KeywordId = row.KeywordId,
                    DocumentFrequency = row.DocumentFrequency,
                    MovieTitleCount = row.MovieTitleCount,
                    TvTitleCount = row.TvTitleCount,
                    Displayable = quality.Displayable,
                    DisplayRank = quality.DisplayRank,
                    UpdatedAtUtc = now,
                });
            }

            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                """TRUNCATE TABLE keyword_display_profiles;""",
                cancellationToken);

            context.KeywordDisplayProfiles.AddRange(profiles);
            await context.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            KeywordDisplayProfileLogMessages.LogRefreshCompleted(
                logger,
                profiles.Count,
                displayableCount,
                stopwatch.ElapsedMilliseconds);

            return new KeywordDisplayProfileRefreshResult(
                Succeeded: true,
                ProfilesWritten: profiles.Count,
                DisplayableCount: displayableCount,
                DurationMilliseconds: stopwatch.ElapsedMilliseconds,
                FailureReason: null);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            KeywordDisplayProfileLogMessages.LogRefreshFailed(logger, exception);
            return new KeywordDisplayProfileRefreshResult(
                Succeeded: false,
                ProfilesWritten: 0,
                DisplayableCount: 0,
                DurationMilliseconds: stopwatch.ElapsedMilliseconds,
                FailureReason: exception.Message);
        }
    }
}
