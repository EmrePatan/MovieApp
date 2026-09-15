using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class KeywordCatalogRepository(ApplicationDbContext dbContext) : IKeywordCatalogRepository
{
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

        await SyncTvShowKeywordsWithContextAsync(tvShow, keywords, syncedAtUtc, cancellationToken);
    }

    private async Task SyncMovieKeywordsWithContextAsync(
        Movie movie,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var linkedKeywordIds = await UpsertKeywordsAndCollectIdsAsync(keywords, syncedAtUtc, cancellationToken);

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
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncTvShowKeywordsWithContextAsync(
        TvShow tvShow,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var linkedKeywordIds = await UpsertKeywordsAndCollectIdsAsync(keywords, syncedAtUtc, cancellationToken);

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
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> UpsertKeywordsAndCollectIdsAsync(
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        if (keywords.Count == 0)
        {
            return [];
        }

        var keywordsByTmdbId = await LoadKeywordsByTmdbIdAsync(keywords, cancellationToken);
        var linkedKeywordIds = new HashSet<Guid>();

        foreach (var keywordSummary in keywords)
        {
            if (!keywordsByTmdbId.TryGetValue(keywordSummary.TmdbKeywordId, out var keyword))
            {
                keyword = new Keyword
                {
                    Id = Guid.NewGuid(),
                    TmdbKeywordId = keywordSummary.TmdbKeywordId,
                    Name = keywordSummary.Name,
                    CreatedAt = syncedAtUtc,
                    UpdatedAt = syncedAtUtc
                };

                dbContext.Keywords.Add(keyword);
                keywordsByTmdbId[keywordSummary.TmdbKeywordId] = keyword;
            }
            else if (!string.Equals(keyword.Name, keywordSummary.Name, StringComparison.Ordinal))
            {
                keyword.Name = keywordSummary.Name;
                keyword.UpdatedAt = syncedAtUtc;
            }

            linkedKeywordIds.Add(keyword.Id);
        }

        return linkedKeywordIds;
    }

    private async Task<Dictionary<int, Keyword>> LoadKeywordsByTmdbIdAsync(
        IReadOnlyList<ProviderKeywordSummary> keywords,
        CancellationToken cancellationToken)
    {
        var tmdbIds = keywords
            .Select(keyword => keyword.TmdbKeywordId)
            .Distinct()
            .ToList();

        var existingKeywords = await dbContext.Keywords
            .Where(keyword => tmdbIds.Contains(keyword.TmdbKeywordId))
            .ToListAsync(cancellationToken);

        return existingKeywords.ToDictionary(keyword => keyword.TmdbKeywordId);
    }
}
