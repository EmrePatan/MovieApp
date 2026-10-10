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

    private static async Task ReconcileProviderTvShowSourcesAsync(
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
}
