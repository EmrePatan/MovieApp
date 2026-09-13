using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class AutocompleteService(
    ISearchRepository searchRepository,
    IUnifiedSearchProviderIngestionService providerIngestionService,
    ICacheService cacheService,
    ILogger<AutocompleteService> logger) : IAutocompleteService
{
    private const int MaxSuggestions = 10;
    private static readonly TimeSpan AutocompleteCacheTtl = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var validation = AdvancedSearchValidator.ValidateQuery(query, required: true);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var cacheKey = SearchAutocompleteCacheKeys.Create(query);
        var cachedEntry = await cacheService.GetAsync<SearchAutocompleteCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Items;
        }

        try
        {
            var items = await providerIngestionService.GetAutocompleteSuggestionsAsync(
                query,
                MaxSuggestions,
                cancellationToken);

            await cacheService.SetAsync(
                cacheKey,
                new SearchAutocompleteCacheEntry { Items = items },
                AutocompleteCacheTtl,
                cancellationToken);

            return items;
        }
        catch (Exception exception)
        {
            AutocompleteServiceLogMessages.LogDbFallback(logger, query, exception);

            var fallbackItems = await searchRepository.AutocompleteAsync(query, MaxSuggestions, cancellationToken);
            return fallbackItems;
        }
    }
}
