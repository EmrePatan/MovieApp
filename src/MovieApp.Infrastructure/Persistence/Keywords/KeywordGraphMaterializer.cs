using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Keywords;

internal static class KeywordGraphMaterializer
{
    public static async Task MaterializeMovieKeywordsUnionAsync(
        ApplicationDbContext dbContext,
        Guid movieId,
        CancellationToken cancellationToken)
    {
        var sourceKeywordIds = await dbContext.MovieKeywordSources
            .AsNoTracking()
            .Where(source => source.MovieId == movieId)
            .Select(source => source.KeywordId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await ApplyMovieKeywordUnionAsync(dbContext, movieId, sourceKeywordIds, cancellationToken);
    }

    public static async Task MaterializeTvShowKeywordsUnionAsync(
        ApplicationDbContext dbContext,
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        var sourceKeywordIds = await dbContext.TvShowKeywordSources
            .AsNoTracking()
            .Where(source => source.TvShowId == tvShowId)
            .Select(source => source.KeywordId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await ApplyTvShowKeywordUnionAsync(dbContext, tvShowId, sourceKeywordIds, cancellationToken);
    }

    public static async Task ReconcileTmdbMovieSourcesAsync(
        ApplicationDbContext dbContext,
        Guid movieId,
        IReadOnlySet<Guid> incomingKeywordIds,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var currentSources = await dbContext.MovieKeywordSources
            .Where(source => source.MovieId == movieId && source.Provider == KeywordProvider.Tmdb)
            .ToListAsync(cancellationToken);

        var currentKeywordIds = currentSources.Select(source => source.KeywordId).ToHashSet();
        var toRemove = currentSources.Where(source => !incomingKeywordIds.Contains(source.KeywordId)).ToList();
        if (toRemove.Count > 0)
        {
            dbContext.MovieKeywordSources.RemoveRange(toRemove);
        }

        foreach (var keywordId in incomingKeywordIds.Where(id => !currentKeywordIds.Contains(id)))
        {
            dbContext.MovieKeywordSources.Add(new MovieKeywordSource
            {
                MovieId = movieId,
                KeywordId = keywordId,
                Provider = KeywordProvider.Tmdb,
                FirstSeenAtUtc = syncedAtUtc,
                LastSeenAtUtc = syncedAtUtc,
            });
        }

        foreach (var source in currentSources.Where(source => incomingKeywordIds.Contains(source.KeywordId)))
        {
            source.LastSeenAtUtc = syncedAtUtc;
        }
    }

    public static async Task ReconcileTmdbTvShowSourcesAsync(
        ApplicationDbContext dbContext,
        Guid tvShowId,
        IReadOnlySet<Guid> incomingKeywordIds,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var currentSources = await dbContext.TvShowKeywordSources
            .Where(source => source.TvShowId == tvShowId && source.Provider == KeywordProvider.Tmdb)
            .ToListAsync(cancellationToken);

        var currentKeywordIds = currentSources.Select(source => source.KeywordId).ToHashSet();
        var toRemove = currentSources.Where(source => !incomingKeywordIds.Contains(source.KeywordId)).ToList();
        if (toRemove.Count > 0)
        {
            dbContext.TvShowKeywordSources.RemoveRange(toRemove);
        }

        foreach (var keywordId in incomingKeywordIds.Where(id => !currentKeywordIds.Contains(id)))
        {
            dbContext.TvShowKeywordSources.Add(new TvShowKeywordSource
            {
                TvShowId = tvShowId,
                KeywordId = keywordId,
                Provider = KeywordProvider.Tmdb,
                FirstSeenAtUtc = syncedAtUtc,
                LastSeenAtUtc = syncedAtUtc,
            });
        }

        foreach (var source in currentSources.Where(source => incomingKeywordIds.Contains(source.KeywordId)))
        {
            source.LastSeenAtUtc = syncedAtUtc;
        }
    }

    private static async Task ApplyMovieKeywordUnionAsync(
        ApplicationDbContext dbContext,
        Guid movieId,
        IReadOnlyCollection<Guid> sourceKeywordIds,
        CancellationToken cancellationToken)
    {
        var currentKeywordIds = await dbContext.MovieKeywords
            .Where(join => join.MovieId == movieId)
            .Select(join => join.KeywordId)
            .ToListAsync(cancellationToken);

        var sourceSet = sourceKeywordIds.ToHashSet();
        var currentSet = currentKeywordIds.ToHashSet();

        var toRemove = currentSet.Except(sourceSet).ToList();
        if (toRemove.Count > 0)
        {
            var joinsToRemove = await dbContext.MovieKeywords
                .Where(join => join.MovieId == movieId && toRemove.Contains(join.KeywordId))
                .ToListAsync(cancellationToken);
            dbContext.MovieKeywords.RemoveRange(joinsToRemove);
        }

        foreach (var keywordId in sourceSet.Except(currentSet))
        {
            dbContext.MovieKeywords.Add(new MovieKeyword
            {
                MovieId = movieId,
                KeywordId = keywordId,
            });
        }
    }

    private static async Task ApplyTvShowKeywordUnionAsync(
        ApplicationDbContext dbContext,
        Guid tvShowId,
        IReadOnlyCollection<Guid> sourceKeywordIds,
        CancellationToken cancellationToken)
    {
        var currentKeywordIds = await dbContext.TvShowKeywords
            .Where(join => join.TvShowId == tvShowId)
            .Select(join => join.KeywordId)
            .ToListAsync(cancellationToken);

        var sourceSet = sourceKeywordIds.ToHashSet();
        var currentSet = currentKeywordIds.ToHashSet();

        var toRemove = currentSet.Except(sourceSet).ToList();
        if (toRemove.Count > 0)
        {
            var joinsToRemove = await dbContext.TvShowKeywords
                .Where(join => join.TvShowId == tvShowId && toRemove.Contains(join.KeywordId))
                .ToListAsync(cancellationToken);
            dbContext.TvShowKeywords.RemoveRange(joinsToRemove);
        }

        foreach (var keywordId in sourceSet.Except(currentSet))
        {
            dbContext.TvShowKeywords.Add(new TvShowKeyword
            {
                TvShowId = tvShowId,
                KeywordId = keywordId,
            });
        }
    }
}
