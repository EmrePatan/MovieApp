using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.Search;

public sealed record ContentSearchTitleProviderEnrichmentRequest
{
    public const int DefaultBatchSize = 10;

    public const int DefaultMaxItems = 50;

    public const int DefaultDelayBetweenRequestsMs = 350;

    public CatalogContentType? ContentType { get; init; }

    public Guid? OnlyMovieId { get; init; }

    public Guid? OnlyTvShowId { get; init; }

    public Guid? StartAfterMovieId { get; init; }

    public Guid? StartAfterTvShowId { get; init; }

    public int BatchSize { get; init; } = DefaultBatchSize;

    public int MaxItems { get; init; } = DefaultMaxItems;

    public int DelayBetweenRequestsMs { get; init; } = DefaultDelayBetweenRequestsMs;
}
