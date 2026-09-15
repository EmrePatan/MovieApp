using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.Collections;

public sealed class GetCollectionService(
    ICollectionDataProvider collectionDataProvider,
    IMovieRepository movieRepository,
    ICacheService cacheService,
    ILogger<GetCollectionService> logger) : IGetCollectionService
{
    private static readonly TimeSpan CollectionCacheTtl = TimeSpan.FromHours(24);

    public async Task<CollectionDetailResult> GetAsync(
        int tmdbCollectionId,
        CancellationToken cancellationToken = default)
    {
        if (tmdbCollectionId <= 0)
        {
            throw new ValidationException("A valid TMDB collection id is required.");
        }

        var cacheKey = CollectionCacheKeys.Create(tmdbCollectionId);
        var cachedEntry = await cacheService.GetAsync<CollectionCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        CollectionProviderDetails? providerDetails;
        try
        {
            providerDetails = await collectionDataProvider.GetCollectionAsync(tmdbCollectionId, cancellationToken);
        }
        catch (Exception exception)
        {
            GetCollectionLogMessages.LogProviderFailed(logger, tmdbCollectionId, exception);
            throw new SearchProviderUnavailableException();
        }

        if (providerDetails is null)
        {
            throw new NotFoundException("The requested collection was not found.");
        }

        var filteredParts = CollectionPartPolicy.Apply(providerDetails.Parts);
        var summaries = new List<MovieProviderSummary>(filteredParts.Count);

        foreach (var part in filteredParts)
        {
            if (!TryCreateSummary(part, out var summary))
            {
                GetCollectionLogMessages.LogMalformedPart(logger, tmdbCollectionId, part.TmdbId);
                continue;
            }

            summaries.Add(summary);
        }

        var catalogIds = await movieRepository.EnsureFromSummariesAsync(summaries, cancellationToken);

        var materializedParts = new List<CollectionPartResult>(summaries.Count);
        foreach (var part in filteredParts)
        {
            if (!TryCreateSummary(part, out var summary) ||
                !summary.TmdbId.HasValue ||
                !catalogIds.TryGetValue(summary.TmdbId.Value, out var catalogId))
            {
                continue;
            }

            materializedParts.Add(new CollectionPartResult(
                catalogId,
                part.TmdbId,
                part.Title,
                part.OriginalTitle,
                part.Overview,
                part.PosterPath,
                part.BackdropPath,
                part.ReleaseDate,
                part.VoteAverage,
                part.VoteCount));
        }

        var result = new CollectionDetailResult(
            providerDetails.TmdbId,
            providerDetails.Name,
            providerDetails.Overview,
            providerDetails.PosterPath,
            providerDetails.BackdropPath,
            materializedParts);

        await cacheService.SetAsync(
            cacheKey,
            new CollectionCacheEntry { Result = result },
            CollectionCacheTtl,
            cancellationToken);

        return result;
    }

    private static bool TryCreateSummary(CollectionProviderPart part, out MovieProviderSummary summary)
    {
        if (part.TmdbId <= 0 || string.IsNullOrWhiteSpace(part.Title))
        {
            summary = null!;
            return false;
        }

        summary = new MovieProviderSummary(
            ExternalId: $"tmdb-{part.TmdbId}",
            TmdbId: part.TmdbId,
            TvdbId: null,
            ImdbId: null,
            Title: part.Title.Trim(),
            Overview: part.Overview,
            ReleaseDate: part.ReleaseDate,
            PosterPath: part.PosterPath,
            VoteAverage: part.VoteAverage,
            VoteCount: part.VoteCount);

        return true;
    }
}
