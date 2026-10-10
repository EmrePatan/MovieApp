using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Keywords;

namespace MovieApp.Infrastructure.Persistence.Keywords;

internal static class KeywordGraphMaterializer
{
    public static Task ReconcileTmdbMovieSourcesAsync(
        ApplicationDbContext dbContext,
        Guid movieId,
        IReadOnlySet<Guid> incomingKeywordIds,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        ReconcileProviderMovieSourcesAsync(
            dbContext,
            movieId,
            KeywordProvider.Tmdb,
            incomingKeywordIds,
            cancellationToken);

    public static Task ReconcileMdbListMovieSourcesAsync(
        ApplicationDbContext dbContext,
        Guid movieId,
        IReadOnlySet<Guid> incomingKeywordIds,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        ReconcileProviderMovieSourcesAsync(
            dbContext,
            movieId,
            KeywordProvider.MdbList,
            incomingKeywordIds,
            cancellationToken);

    public static Task ReconcileMdbListTvShowSourcesAsync(
        ApplicationDbContext dbContext,
        Guid tvShowId,
        IReadOnlySet<Guid> incomingKeywordIds,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        ReconcileProviderTvShowSourcesAsync(
            dbContext,
            tvShowId,
            KeywordProvider.MdbList,
            incomingKeywordIds,
            cancellationToken);

    public static Task ReconcileTmdbTvShowSourcesAsync(
        ApplicationDbContext dbContext,
        Guid tvShowId,
        IReadOnlySet<Guid> incomingKeywordIds,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        ReconcileProviderTvShowSourcesAsync(
            dbContext,
            tvShowId,
            KeywordProvider.Tmdb,
            incomingKeywordIds,
            cancellationToken);

    private static async Task ReconcileProviderMovieSourcesAsync(
        ApplicationDbContext dbContext,
        Guid movieId,
        KeywordProvider provider,
        IReadOnlySet<Guid> incomingKeywordIds,
        CancellationToken cancellationToken)
    {
        if (IsNpgsql(dbContext))
        {
            await ReconcileProviderMovieSourcesPostgresAsync(
                dbContext,
                movieId,
                provider,
                incomingKeywordIds,
                cancellationToken);
            return;
        }

        await ReconcileProviderMovieSourcesTrackedAsync(
            dbContext,
            movieId,
            provider,
            incomingKeywordIds,
            cancellationToken);
    }

    private static async Task ReconcileProviderTvShowSourcesAsync(
        ApplicationDbContext dbContext,
        Guid tvShowId,
        KeywordProvider provider,
        IReadOnlySet<Guid> incomingKeywordIds,
        CancellationToken cancellationToken)
    {
        if (IsNpgsql(dbContext))
        {
            await ReconcileProviderTvShowSourcesPostgresAsync(
                dbContext,
                tvShowId,
                provider,
                incomingKeywordIds,
                cancellationToken);
            return;
        }

        await ReconcileProviderTvShowSourcesTrackedAsync(
            dbContext,
            tvShowId,
            provider,
            incomingKeywordIds,
            cancellationToken);
    }

    private static async Task ReconcileProviderMovieSourcesPostgresAsync(
        ApplicationDbContext dbContext,
        Guid movieId,
        KeywordProvider provider,
        IReadOnlySet<Guid> incomingKeywordIds,
        CancellationToken cancellationToken)
    {
        var providerName = KeywordProviderSources.ToStorageName(provider);
        var keywordIds = incomingKeywordIds.ToArray();

        if (keywordIds.Length == 0)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE movie_keywords
                SET "Sources" = "Sources" - {providerName}
                WHERE "MovieId" = {movieId}
                  AND "Sources" ? {providerName};

                DELETE FROM movie_keywords
                WHERE "MovieId" = {movieId}
                  AND "Sources" = '[]'::jsonb;
                """,
                cancellationToken);
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE movie_keywords
            SET "Sources" = "Sources" - {providerName}
            WHERE "MovieId" = {movieId}
              AND "Sources" ? {providerName}
              AND NOT ("KeywordId" = ANY({keywordIds}));

            DELETE FROM movie_keywords
            WHERE "MovieId" = {movieId}
              AND "Sources" = '[]'::jsonb;

            INSERT INTO movie_keywords ("MovieId", "KeywordId", "Sources")
            SELECT {movieId}, incoming."KeywordId", jsonb_build_array({providerName})
            FROM unnest({keywordIds}) AS incoming("KeywordId")
            ON CONFLICT ("MovieId", "KeywordId") DO UPDATE
            SET "Sources" = CASE
                WHEN movie_keywords."Sources" ? {providerName}
                    THEN movie_keywords."Sources"
                ELSE movie_keywords."Sources" || jsonb_build_array({providerName})
            END;
            """,
            cancellationToken);
    }

    private static async Task ReconcileProviderTvShowSourcesPostgresAsync(
        ApplicationDbContext dbContext,
        Guid tvShowId,
        KeywordProvider provider,
        IReadOnlySet<Guid> incomingKeywordIds,
        CancellationToken cancellationToken)
    {
        var providerName = KeywordProviderSources.ToStorageName(provider);
        var keywordIds = incomingKeywordIds.ToArray();

        if (keywordIds.Length == 0)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE tv_show_keywords
                SET "Sources" = "Sources" - {providerName}
                WHERE "TvShowId" = {tvShowId}
                  AND "Sources" ? {providerName};

                DELETE FROM tv_show_keywords
                WHERE "TvShowId" = {tvShowId}
                  AND "Sources" = '[]'::jsonb;
                """,
                cancellationToken);
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE tv_show_keywords
            SET "Sources" = "Sources" - {providerName}
            WHERE "TvShowId" = {tvShowId}
              AND "Sources" ? {providerName}
              AND NOT ("KeywordId" = ANY({keywordIds}));

            DELETE FROM tv_show_keywords
            WHERE "TvShowId" = {tvShowId}
              AND "Sources" = '[]'::jsonb;

            INSERT INTO tv_show_keywords ("TvShowId", "KeywordId", "Sources")
            SELECT {tvShowId}, incoming."KeywordId", jsonb_build_array({providerName})
            FROM unnest({keywordIds}) AS incoming("KeywordId")
            ON CONFLICT ("TvShowId", "KeywordId") DO UPDATE
            SET "Sources" = CASE
                WHEN tv_show_keywords."Sources" ? {providerName}
                    THEN tv_show_keywords."Sources"
                ELSE tv_show_keywords."Sources" || jsonb_build_array({providerName})
            END;
            """,
            cancellationToken);
    }

    private static async Task ReconcileProviderMovieSourcesTrackedAsync(
        ApplicationDbContext dbContext,
        Guid movieId,
        KeywordProvider provider,
        IReadOnlySet<Guid> incomingKeywordIds,
        CancellationToken cancellationToken)
    {
        var currentRelationships = await dbContext.MovieKeywords
            .Where(relationship => relationship.MovieId == movieId)
            .ToListAsync(cancellationToken);
        var byKeywordId = currentRelationships.ToDictionary(relationship => relationship.KeywordId);

        foreach (var relationship in currentRelationships)
        {
            var providerIsPresent = KeywordProviderSources.Contains(relationship.Sources, provider);
            var providerShouldBePresent = incomingKeywordIds.Contains(relationship.KeywordId);
            if (providerIsPresent == providerShouldBePresent)
            {
                continue;
            }

            var updatedSources = KeywordProviderSources.SetProvider(
                relationship.Sources,
                provider,
                providerShouldBePresent);

            if (!KeywordProviderSources.HasAny(updatedSources))
            {
                dbContext.MovieKeywords.Remove(relationship);
                byKeywordId.Remove(relationship.KeywordId);
                continue;
            }

            relationship.Sources = updatedSources;
        }

        foreach (var keywordId in incomingKeywordIds)
        {
            if (byKeywordId.TryGetValue(keywordId, out var existingRelationship))
            {
                if (!KeywordProviderSources.Contains(existingRelationship.Sources, provider))
                {
                    existingRelationship.Sources = KeywordProviderSources.SetProvider(
                        existingRelationship.Sources,
                        provider,
                        include: true);
                }

                continue;
            }

            var relationship = new MovieKeyword
            {
                MovieId = movieId,
                KeywordId = keywordId,
                Sources = KeywordProviderSources.Create(provider),
            };
            dbContext.MovieKeywords.Add(relationship);
            byKeywordId[keywordId] = relationship;
        }
    }

    private static async Task ReconcileProviderTvShowSourcesTrackedAsync(
        ApplicationDbContext dbContext,
        Guid tvShowId,
        KeywordProvider provider,
        IReadOnlySet<Guid> incomingKeywordIds,
        CancellationToken cancellationToken)
    {
        var currentRelationships = await dbContext.TvShowKeywords
            .Where(relationship => relationship.TvShowId == tvShowId)
            .ToListAsync(cancellationToken);
        var byKeywordId = currentRelationships.ToDictionary(relationship => relationship.KeywordId);

        foreach (var relationship in currentRelationships)
        {
            var providerIsPresent = KeywordProviderSources.Contains(relationship.Sources, provider);
            var providerShouldBePresent = incomingKeywordIds.Contains(relationship.KeywordId);
            if (providerIsPresent == providerShouldBePresent)
            {
                continue;
            }

            var updatedSources = KeywordProviderSources.SetProvider(
                relationship.Sources,
                provider,
                providerShouldBePresent);

            if (!KeywordProviderSources.HasAny(updatedSources))
            {
                dbContext.TvShowKeywords.Remove(relationship);
                byKeywordId.Remove(relationship.KeywordId);
                continue;
            }

            relationship.Sources = updatedSources;
        }

        foreach (var keywordId in incomingKeywordIds)
        {
            if (byKeywordId.TryGetValue(keywordId, out var existingRelationship))
            {
                if (!KeywordProviderSources.Contains(existingRelationship.Sources, provider))
                {
                    existingRelationship.Sources = KeywordProviderSources.SetProvider(
                        existingRelationship.Sources,
                        provider,
                        include: true);
                }

                continue;
            }

            var relationship = new TvShowKeyword
            {
                TvShowId = tvShowId,
                KeywordId = keywordId,
                Sources = KeywordProviderSources.Create(provider),
            };
            dbContext.TvShowKeywords.Add(relationship);
            byKeywordId[keywordId] = relationship;
        }
    }

    private static bool IsNpgsql(ApplicationDbContext dbContext) =>
        dbContext.Database.IsRelational() &&
        dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;
}
