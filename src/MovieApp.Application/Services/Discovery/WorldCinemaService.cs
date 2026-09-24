using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Discovery;

public sealed class WorldCinemaService(
    IAdvancedDiscoverService advancedDiscoverService,
    ICacheService cacheService) : IWorldCinemaService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private const int TopRatedMovieMinimumVoteCount = 200;
    private const int TopRatedTvMinimumVoteCount = 100;

    public async Task<PaginatedResult<SearchItem>> GetWorldCinemaAsync(
        WorldCinemaCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var validation = WorldCinemaValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var cacheKey = WorldCinemaCacheKeys.Create(criteria, contentLocale);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        PaginatedResult<SearchItem> result;
        try
        {
            result = await advancedDiscoverService.DiscoverAsync(
                ToAdvancedDiscoverCriteria(criteria),
                contentLocale,
                cancellationToken);
        }
        catch (SearchProviderUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            throw new SearchProviderUnavailableException();
        }

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            CacheTtl,
            cancellationToken);

        return result;
    }

    internal static AdvancedDiscoverCriteria ToAdvancedDiscoverCriteria(WorldCinemaCriteria criteria) =>
        new(
            criteria.MediaType,
            [],
            GenreMatchMode.All,
            null,
            null,
            null,
            null,
            null,
            ResolveTopRatedMinimumVoteCount(criteria.MediaType, criteria.Sort),
            null,
            null,
            null,
            criteria.OriginCountry.Trim().ToUpperInvariant(),
            null,
            null,
            [],
            null,
            [],
            [],
            criteria.Sort,
            criteria.Page,
            criteria.PageSize);

    internal static int? ResolveTopRatedMinimumVoteCount(
        SearchContentType mediaType,
        AdvancedDiscoverSort sort) =>
        sort switch
        {
            AdvancedDiscoverSort.RatingDesc => mediaType switch
            {
                SearchContentType.Tv => TopRatedTvMinimumVoteCount,
                _ => TopRatedMovieMinimumVoteCount
            },
            _ => null
        };
}
