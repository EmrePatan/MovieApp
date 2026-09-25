using Microsoft.Extensions.Logging;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static partial class SearchServiceLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Unified search cache hit for query {Query} type {ContentType} page {Page}.")]
    internal static partial void LogCacheHit(
        ILogger logger,
        string? query,
        SearchContentType contentType,
        int page);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Unified search served from database fallback for query {Query} type {ContentType} page {Page}.")]
    internal static partial void LogDbFallback(
        ILogger logger,
        string? query,
        SearchContentType contentType,
        int page);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Unified search provider failed with no database fallback for query {Query} type {ContentType} page {Page}.")]
    internal static partial void LogProviderUnavailable(
        ILogger logger,
        string? query,
        SearchContentType contentType,
        int page);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Search history write failed for user {UserId}.")]
    internal static partial void LogSearchHistoryFailed(
        ILogger logger,
        Guid userId,
        Exception exception);
}
