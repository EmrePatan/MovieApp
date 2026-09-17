using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class UserRecommendationContextModels
{
    internal const int MaxSearchQueries = 10;

    internal sealed record RatingRow(
        Guid? MovieId,
        Guid? TvShowId,
        int Score,
        DateTime SignalAtUtc,
        string? MovieTitle,
        string? TvShowTitle);

    internal sealed record TimestampedContentRow(
        Guid? MovieId,
        Guid? TvShowId,
        DateTime SignalAtUtc,
        string? MovieTitle,
        string? TvShowTitle);

    internal sealed record WatchedMovieRow(Guid MovieId, string Title, DateTime WatchedAt);

    internal sealed record WatchedEpisodeRow(Guid TvShowId, DateTime WatchedAt);

    internal sealed record CatalogFollowRow(
        CatalogContentType ContentType,
        Guid ContentId,
        DateTime FollowedAt);

    internal sealed record SearchMatchRow(Guid Id, string Title, int VoteCount);

    internal sealed record SignalSeed(
        Guid ContentId,
        string ContentType,
        string SignalType,
        string Title,
        int? RatingScore,
        DateTime? SignalAtUtc);

    internal sealed record GenreRow(Guid ContentId, Guid GenreId, string GenreName);

    internal sealed record PersonRow(Guid ContentId, List<Guid> PersonIds);

    internal sealed record KeywordRow(Guid ContentId, Guid KeywordId);

    internal sealed record CatalogMetadataRow(Guid ContentId, decimal VoteAverage, int? Year);

    internal sealed record MovieSignalProjection(
        Guid Id,
        decimal VoteAverage,
        int? Year,
        List<GenreRow> Genres,
        List<KeywordRow> Keywords,
        List<Guid> PersonIds);

    internal sealed record TvSignalProjection(
        Guid Id,
        decimal VoteAverage,
        int? Year,
        List<GenreRow> Genres,
        List<KeywordRow> Keywords,
        List<Guid> PersonIds);
}
