using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence.Keywords;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class KeywordCatalogRepository(
    ApplicationDbContext dbContext,
    IOptions<KeywordGraphOptions> keywordGraphOptions) : IKeywordCatalogRepository
{
    private readonly bool _providerAwareSyncEnabled = keywordGraphOptions.Value.ProviderAwareSyncEnabled;

    public async Task<KeywordEnrichmentTarget?> GetMovieKeywordTargetAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var movie = await dbContext.Movies
            .AsNoTracking()
            .Where(existingMovie => existingMovie.Id == movieId)
            .Select(existingMovie => new { existingMovie.TmdbId, existingMovie.KeywordsSyncedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (movie is null || movie.TmdbId is null or <= 0)
        {
            return null;
        }

        return new KeywordEnrichmentTarget(movie.TmdbId.Value, movie.KeywordsSyncedAtUtc);
    }

    public async Task<KeywordEnrichmentTarget?> GetTvShowKeywordTargetAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await dbContext.TvShows
            .AsNoTracking()
            .Where(existingTvShow => existingTvShow.Id == tvShowId)
            .Select(existingTvShow => new { existingTvShow.TmdbId, existingTvShow.KeywordsSyncedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (tvShow is null || tvShow.TmdbId is null or <= 0)
        {
            return null;
        }

        return new KeywordEnrichmentTarget(tvShow.TmdbId.Value, tvShow.KeywordsSyncedAtUtc);
    }

    public async Task SyncMovieKeywordsAsync(
        Guid movieId,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var movie = await dbContext.Movies
            .Include(existingMovie => existingMovie.MovieKeywords)
            .FirstOrDefaultAsync(existingMovie => existingMovie.Id == movieId, cancellationToken);

        if (movie is null)
        {
            return;
        }

        if (_providerAwareSyncEnabled)
        {
            await SyncMovieKeywordsProviderAwareAsync(movie, keywords, syncedAtUtc, cancellationToken);
            return;
        }

        await SyncMovieKeywordsWithContextAsync(movie, keywords, syncedAtUtc, cancellationToken);
    }

    public async Task SyncTvShowKeywordsAsync(
        Guid tvShowId,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await dbContext.TvShows
            .Include(existingTvShow => existingTvShow.TvShowKeywords)
            .FirstOrDefaultAsync(existingTvShow => existingTvShow.Id == tvShowId, cancellationToken);

        if (tvShow is null)
        {
            return;
        }

        if (_providerAwareSyncEnabled)
        {
            await SyncTvShowKeywordsProviderAwareAsync(tvShow, keywords, syncedAtUtc, cancellationToken);
            return;
        }

        await SyncTvShowKeywordsWithContextAsync(tvShow, keywords, syncedAtUtc, cancellationToken);
    }

    private async Task SyncMovieKeywordsProviderAwareAsync(
        Movie movie,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var dedupedKeywords = DeduplicateKeywords(keywords);

        if (IsNpgsql())
        {
            await dbContext.Database.ExecuteInRetriableTransactionAsync(
                async ct =>
                {
                    var incomingKeywordIds = await ResolveIncomingKeywordIdsAsync(dedupedKeywords, syncedAtUtc, ct);
                    await KeywordGraphMaterializer.ReconcileTmdbMovieSourcesAsync(
                        dbContext,
                        movie.Id,
                        incomingKeywordIds,
                        syncedAtUtc,
                        ct);
                    await dbContext.SaveChangesAsync(ct);
                    await KeywordGraphMaterializer.MaterializeMovieKeywordsUnionAsync(dbContext, movie.Id, ct);
                    movie.KeywordsSyncedAtUtc = syncedAtUtc;
                    await dbContext.SaveChangesAsync(ct);
                },
                cancellationToken);
            return;
        }

        var inMemoryIncoming = await ResolveIncomingKeywordIdsAsync(dedupedKeywords, syncedAtUtc, cancellationToken);
        await KeywordGraphMaterializer.ReconcileTmdbMovieSourcesAsync(
            dbContext,
            movie.Id,
            inMemoryIncoming,
            syncedAtUtc,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await KeywordGraphMaterializer.MaterializeMovieKeywordsUnionAsync(dbContext, movie.Id, cancellationToken);
        movie.KeywordsSyncedAtUtc = syncedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncTvShowKeywordsProviderAwareAsync(
        TvShow tvShow,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var dedupedKeywords = DeduplicateKeywords(keywords);

        if (IsNpgsql())
        {
            await dbContext.Database.ExecuteInRetriableTransactionAsync(
                async ct =>
                {
                    var incomingKeywordIds = await ResolveIncomingKeywordIdsAsync(dedupedKeywords, syncedAtUtc, ct);
                    await KeywordGraphMaterializer.ReconcileTmdbTvShowSourcesAsync(
                        dbContext,
                        tvShow.Id,
                        incomingKeywordIds,
                        syncedAtUtc,
                        ct);
                    await dbContext.SaveChangesAsync(ct);
                    await KeywordGraphMaterializer.MaterializeTvShowKeywordsUnionAsync(dbContext, tvShow.Id, ct);
                    tvShow.KeywordsSyncedAtUtc = syncedAtUtc;
                    await dbContext.SaveChangesAsync(ct);
                },
                cancellationToken);
            return;
        }

        var inMemoryIncoming = await ResolveIncomingKeywordIdsAsync(dedupedKeywords, syncedAtUtc, cancellationToken);
        await KeywordGraphMaterializer.ReconcileTmdbTvShowSourcesAsync(
            dbContext,
            tvShow.Id,
            inMemoryIncoming,
            syncedAtUtc,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await KeywordGraphMaterializer.MaterializeTvShowKeywordsUnionAsync(dbContext, tvShow.Id, cancellationToken);
        tvShow.KeywordsSyncedAtUtc = syncedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> ResolveIncomingKeywordIdsAsync(
        List<ProviderKeywordSummary> dedupedKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        if (dedupedKeywords.Count == 0)
        {
            return [];
        }

        return await EnsureCanonicalKeywordIdsAsync(dedupedKeywords, syncedAtUtc, cancellationToken);
    }

    private Task SyncMovieKeywordsWithContextAsync(
        Movie movie,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        SynchronizeTitleKeywordsAsync(
            keywords,
            syncedAtUtc,
            async linkedKeywordIds =>
            {
                var relationshipsToRemove = movie.MovieKeywords
                    .Where(movieKeyword => !linkedKeywordIds.Contains(movieKeyword.KeywordId))
                    .ToList();

                foreach (var movieKeyword in relationshipsToRemove)
                {
                    movie.MovieKeywords.Remove(movieKeyword);
                }

                foreach (var keywordId in linkedKeywordIds)
                {
                    if (movie.MovieKeywords.Any(movieKeyword => movieKeyword.KeywordId == keywordId))
                    {
                        continue;
                    }

                    movie.MovieKeywords.Add(new MovieKeyword
                    {
                        MovieId = movie.Id,
                        KeywordId = keywordId
                    });
                }

                movie.KeywordsSyncedAtUtc = syncedAtUtc;
                await MirrorTmdbMovieSourcesForLegacySyncAsync(
                    movie.Id,
                    linkedKeywordIds,
                    syncedAtUtc,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);

    private Task SyncTvShowKeywordsWithContextAsync(
        TvShow tvShow,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        SynchronizeTitleKeywordsAsync(
            keywords,
            syncedAtUtc,
            async linkedKeywordIds =>
            {
                var relationshipsToRemove = tvShow.TvShowKeywords
                    .Where(tvShowKeyword => !linkedKeywordIds.Contains(tvShowKeyword.KeywordId))
                    .ToList();

                foreach (var tvShowKeyword in relationshipsToRemove)
                {
                    tvShow.TvShowKeywords.Remove(tvShowKeyword);
                }

                foreach (var keywordId in linkedKeywordIds)
                {
                    if (tvShow.TvShowKeywords.Any(tvShowKeyword => tvShowKeyword.KeywordId == keywordId))
                    {
                        continue;
                    }

                    tvShow.TvShowKeywords.Add(new TvShowKeyword
                    {
                        TvShowId = tvShow.Id,
                        KeywordId = keywordId
                    });
                }

                tvShow.KeywordsSyncedAtUtc = syncedAtUtc;
                await MirrorTmdbTvShowSourcesForLegacySyncAsync(
                    tvShow.Id,
                    linkedKeywordIds,
                    syncedAtUtc,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);

    private Task MirrorTmdbMovieSourcesForLegacySyncAsync(
        Guid movieId,
        IReadOnlySet<Guid> tmdbKeywordIds,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        KeywordGraphMaterializer.ReconcileTmdbMovieSourcesAsync(
            dbContext,
            movieId,
            tmdbKeywordIds,
            syncedAtUtc,
            cancellationToken);

    private Task MirrorTmdbTvShowSourcesForLegacySyncAsync(
        Guid tvShowId,
        IReadOnlySet<Guid> tmdbKeywordIds,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        KeywordGraphMaterializer.ReconcileTmdbTvShowSourcesAsync(
            dbContext,
            tvShowId,
            tmdbKeywordIds,
            syncedAtUtc,
            cancellationToken);

    private async Task SynchronizeTitleKeywordsAsync(
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        Func<HashSet<Guid>, Task> synchronizeRelationships,
        CancellationToken cancellationToken)
    {
        if (IsNpgsql())
        {
            await dbContext.Database.ExecuteInRetriableTransactionAsync(
                async ct =>
                {
                    var canonicalKeywordIds = await EnsureCanonicalKeywordIdsAsync(keywords, syncedAtUtc, ct);
                    await synchronizeRelationships(canonicalKeywordIds);
                },
                cancellationToken);
            return;
        }

        var inMemoryCanonicalKeywordIds = await EnsureCanonicalKeywordIdsAsync(keywords, syncedAtUtc, cancellationToken);
        await synchronizeRelationships(inMemoryCanonicalKeywordIds);
    }

    private async Task<HashSet<Guid>> EnsureCanonicalKeywordIdsAsync(
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var dedupedKeywords = DeduplicateKeywords(keywords);
        if (dedupedKeywords.Count == 0)
        {
            return [];
        }

        var existingKeywords = await LoadKeywordsByTmdbIdAsync(dedupedKeywords, cancellationToken);
        var missingKeywords = dedupedKeywords
            .Where(keyword => !existingKeywords.ContainsKey(keyword.TmdbKeywordId))
            .ToList();

        Dictionary<int, Keyword> canonicalKeywords;
        if (missingKeywords.Count > 0 && IsNpgsql())
        {
            await KeywordGraphTmdbKeywordDualWrite.InsertMissingKeywordsWithDualWriteAsync(
                dbContext,
                missingKeywords,
                syncedAtUtc,
                cancellationToken);
            canonicalKeywords = await LoadKeywordsByTmdbIdAsync(dedupedKeywords, cancellationToken);
        }
        else
        {
            if (missingKeywords.Count > 0)
            {
                AddMissingKeywordsToContext(missingKeywords, syncedAtUtc, existingKeywords);
            }

            canonicalKeywords = existingKeywords;
        }

        if (!IsNpgsql())
        {
            KeywordGraphTmdbKeywordDualWrite.ApplyMetadataAndNameUpdates(
                canonicalKeywords,
                dedupedKeywords,
                syncedAtUtc);
            await KeywordGraphTmdbKeywordDualWrite.EnsureTmdbExternalReferencesAsync(
                dbContext,
                canonicalKeywords.Values.ToList(),
                syncedAtUtc,
                cancellationToken);
            foreach (var keyword in canonicalKeywords.Values)
            {
                await KeywordGraphTmdbKeywordDualWrite.AssertTmdbExternalReferenceOwnershipAsync(
                    dbContext,
                    keyword,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var tmdbIds = dedupedKeywords.Select(summary => summary.TmdbKeywordId).Distinct().ToList();
            var trackedKeywords = await dbContext.Keywords
                .Where(keyword => keyword.TmdbKeywordId != null && tmdbIds.Contains(keyword.TmdbKeywordId.Value))
                .ToListAsync(cancellationToken);
            var trackedByTmdbId = trackedKeywords.ToDictionary(keyword => keyword.TmdbKeywordId!.Value);
            KeywordGraphTmdbKeywordDualWrite.ApplyMetadataAndNameUpdates(
                trackedByTmdbId,
                dedupedKeywords,
                syncedAtUtc);
            await KeywordGraphTmdbKeywordDualWrite.EnsureTmdbExternalReferencesAsync(
                dbContext,
                trackedKeywords,
                syncedAtUtc,
                cancellationToken);
            foreach (var keyword in trackedKeywords)
            {
                await KeywordGraphTmdbKeywordDualWrite.AssertTmdbExternalReferenceOwnershipAsync(
                    dbContext,
                    keyword,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            canonicalKeywords = trackedByTmdbId;
        }

        return canonicalKeywords.Values
            .Select(keyword => keyword.Id)
            .ToHashSet();
    }

    private void AddMissingKeywordsToContext(
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        Dictionary<int, Keyword> keywordsByTmdbId)
    {
        foreach (var keywordSummary in keywords)
        {
            var keyword = new Keyword
            {
                Id = Guid.NewGuid(),
            };

            KeywordGraphTmdbKeywordDualWrite.PrepareNewKeywordEntity(keyword, keywordSummary, syncedAtUtc);
            dbContext.Keywords.Add(keyword);
            keywordsByTmdbId[keywordSummary.TmdbKeywordId] = keyword;
        }
    }

    private static List<ProviderKeywordSummary> DeduplicateKeywords(IReadOnlyList<ProviderKeywordSummary> keywords) =>
        keywords
            .GroupBy(keyword => keyword.TmdbKeywordId)
            .Select(group => group.Last())
            .ToList();

    private bool IsNpgsql() =>
        dbContext.Database.IsRelational() &&
        dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;

    private async Task<Dictionary<int, Keyword>> LoadKeywordsByTmdbIdAsync(
        IReadOnlyList<ProviderKeywordSummary> keywords,
        CancellationToken cancellationToken)
    {
        var tmdbIds = keywords
            .Select(keyword => keyword.TmdbKeywordId)
            .Distinct()
            .ToList();

        var existingKeywords = await dbContext.Keywords
            .Where(keyword => keyword.TmdbKeywordId != null && tmdbIds.Contains(keyword.TmdbKeywordId.Value))
            .ToListAsync(cancellationToken);

        return existingKeywords.ToDictionary(keyword => keyword.TmdbKeywordId!.Value);
    }

    public async Task<KeywordEnrichmentTarget?> GetMovieMdbListKeywordTargetAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var movie = await dbContext.Movies
            .AsNoTracking()
            .Where(existingMovie => existingMovie.Id == movieId)
            .Select(existingMovie => new { existingMovie.TmdbId, existingMovie.MdbListKeywordsSyncedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (movie is null || movie.TmdbId is null or <= 0)
        {
            return null;
        }

        return new KeywordEnrichmentTarget(movie.TmdbId.Value, movie.MdbListKeywordsSyncedAtUtc);
    }

    public async Task<KeywordEnrichmentTarget?> GetTvShowMdbListKeywordTargetAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await dbContext.TvShows
            .AsNoTracking()
            .Where(existingTvShow => existingTvShow.Id == tvShowId)
            .Select(existingTvShow => new { existingTvShow.TmdbId, existingTvShow.MdbListKeywordsSyncedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (tvShow is null || tvShow.TmdbId is null or <= 0)
        {
            return null;
        }

        return new KeywordEnrichmentTarget(tvShow.TmdbId.Value, tvShow.MdbListKeywordsSyncedAtUtc);
    }

    public async Task<Guid?> FindMovieIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.TmdbId == tmdbId)
            .Select(movie => (Guid?)movie.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Guid?> FindTvShowIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.TmdbId == tmdbId)
            .Select(tvShow => (Guid?)tvShow.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<MdbListKeywordIngestionResult> ApplyMovieMdbListKeywordIngestionAsync(
        Guid movieId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        ApplyMdbListKeywordIngestionAsync(
            movieId,
            isMovie: true,
            providerKeywords,
            syncedAtUtc,
            cancellationToken);

    public Task<MdbListKeywordIngestionResult> ApplyTvShowMdbListKeywordIngestionAsync(
        Guid tvShowId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken) =>
        ApplyMdbListKeywordIngestionAsync(
            tvShowId,
            isMovie: false,
            providerKeywords,
            syncedAtUtc,
            cancellationToken);

    public async Task<int> CountMovieMdbListSourcesAsync(Guid movieId, CancellationToken cancellationToken = default) =>
        await dbContext.MovieKeywordSources
            .AsNoTracking()
            .CountAsync(
                source => source.MovieId == movieId && source.Provider == Domain.Enums.KeywordProvider.MdbList,
                cancellationToken);

    public async Task<int> CountTvShowMdbListSourcesAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
        await dbContext.TvShowKeywordSources
            .AsNoTracking()
            .CountAsync(
                source => source.TvShowId == tvShowId && source.Provider == Domain.Enums.KeywordProvider.MdbList,
                cancellationToken);

    private async Task<MdbListKeywordIngestionResult> ApplyMdbListKeywordIngestionAsync(
        Guid contentId,
        bool isMovie,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        if (isMovie)
        {
            var movie = await dbContext.Movies.FirstOrDefaultAsync(existing => existing.Id == contentId, cancellationToken);
            if (movie is null)
            {
                return new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.CatalogNotFound);
            }

            MdbListKeywordResolutionStats stats;
            if (IsNpgsql())
            {
                stats = await dbContext.Database.ExecuteInRetriableTransactionAsync(
                    async ct =>
                    {
                        var resolution = await MdbListKeywordGraphResolver.ResolveAsync(
                            dbContext,
                            providerKeywords,
                            syncedAtUtc,
                            ct);
                        await dbContext.SaveChangesAsync(ct);
                        await KeywordGraphMaterializer.ReconcileMdbListMovieSourcesAsync(
                            dbContext,
                            movie.Id,
                            resolution.PromotedKeywordIds,
                            syncedAtUtc,
                            ct);
                        await dbContext.SaveChangesAsync(ct);
                        await KeywordGraphMaterializer.MaterializeMovieKeywordsUnionAsync(dbContext, movie.Id, ct);
                        movie.MdbListKeywordsSyncedAtUtc = syncedAtUtc;
                        await dbContext.SaveChangesAsync(ct);
                        return resolution.Stats;
                    },
                    cancellationToken);
            }
            else
            {
                var resolution = await MdbListKeywordGraphResolver.ResolveAsync(
                    dbContext,
                    providerKeywords,
                    syncedAtUtc,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await KeywordGraphMaterializer.ReconcileMdbListMovieSourcesAsync(
                    dbContext,
                    movie.Id,
                    resolution.PromotedKeywordIds,
                    syncedAtUtc,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await KeywordGraphMaterializer.MaterializeMovieKeywordsUnionAsync(dbContext, movie.Id, cancellationToken);
                movie.MdbListKeywordsSyncedAtUtc = syncedAtUtc;
                await dbContext.SaveChangesAsync(cancellationToken);
                stats = resolution.Stats;
            }

            var sourceCount = await CountMovieMdbListSourcesAsync(movie.Id, cancellationToken);
            return new MdbListKeywordIngestionResult(
                MdbListKeywordIngestionStatus.Succeeded,
                stats,
                sourceCount,
                movie.MdbListKeywordsSyncedAtUtc);
        }

        var tvShow = await dbContext.TvShows.FirstOrDefaultAsync(existing => existing.Id == contentId, cancellationToken);
        if (tvShow is null)
        {
            return new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.CatalogNotFound);
        }

        MdbListKeywordResolutionStats tvStats;
        if (IsNpgsql())
        {
            tvStats = await dbContext.Database.ExecuteInRetriableTransactionAsync(
                async ct =>
                {
                    var resolution = await MdbListKeywordGraphResolver.ResolveAsync(
                        dbContext,
                        providerKeywords,
                        syncedAtUtc,
                        ct);
                    await dbContext.SaveChangesAsync(ct);
                    await KeywordGraphMaterializer.ReconcileMdbListTvShowSourcesAsync(
                        dbContext,
                        tvShow.Id,
                        resolution.PromotedKeywordIds,
                        syncedAtUtc,
                        ct);
                    await dbContext.SaveChangesAsync(ct);
                    await KeywordGraphMaterializer.MaterializeTvShowKeywordsUnionAsync(dbContext, tvShow.Id, ct);
                    tvShow.MdbListKeywordsSyncedAtUtc = syncedAtUtc;
                    await dbContext.SaveChangesAsync(ct);
                    return resolution.Stats;
                },
                cancellationToken);
        }
        else
        {
            var resolution = await MdbListKeywordGraphResolver.ResolveAsync(
                dbContext,
                providerKeywords,
                syncedAtUtc,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await KeywordGraphMaterializer.ReconcileMdbListTvShowSourcesAsync(
                dbContext,
                tvShow.Id,
                resolution.PromotedKeywordIds,
                syncedAtUtc,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await KeywordGraphMaterializer.MaterializeTvShowKeywordsUnionAsync(dbContext, tvShow.Id, cancellationToken);
            tvShow.MdbListKeywordsSyncedAtUtc = syncedAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken);
            tvStats = resolution.Stats;
        }

        var tvSourceCount = await CountTvShowMdbListSourcesAsync(tvShow.Id, cancellationToken);
        return new MdbListKeywordIngestionResult(
            MdbListKeywordIngestionStatus.Succeeded,
            tvStats,
            tvSourceCount,
            tvShow.MdbListKeywordsSyncedAtUtc);
    }
}
