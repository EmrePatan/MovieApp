using Microsoft.Extensions.Logging;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static partial class UnifiedSearchProviderIngestionLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Skipped unified search movie ingestion for external ID {ExternalId} (TMDB {TmdbId}) due to persistence conflict.")]
    internal static partial void LogSkippedMoviePersistenceConflict(
        ILogger logger,
        string externalId,
        int? tmdbId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Unified search movie provider search failed for query {Query} page {Page}.")]
    internal static partial void LogMovieSearchFailed(
        ILogger logger,
        string query,
        int page,
        Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Unified search TV provider search failed for query {Query} page {Page}.")]
    internal static partial void LogTvSearchFailed(
        ILogger logger,
        string query,
        int page,
        Exception exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Unified search provider search succeeded for query {Query} type {ContentType} page {Page} with {TotalCount} total results.")]
    internal static partial void LogProviderSearchSucceeded(
        ILogger logger,
        string query,
        SearchContentType contentType,
        int page,
        int totalCount);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Autocomplete provider search succeeded for query {Query} with {SuggestionCount} suggestions.")]
    internal static partial void LogAutocompleteProviderSucceeded(
        ILogger logger,
        string query,
        int suggestionCount);
}
