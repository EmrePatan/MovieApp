using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiTasteProfileDataSource
{
    Task<AiTasteProfileRawData> LoadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetWatchedMovieIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetWatchedTvShowIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record AiTasteProfileRawData(
    IReadOnlyList<AiRatingRow> Ratings,
    IReadOnlyList<AiMovieTitleRow> Favorites,
    IReadOnlyList<AiMovieTitleRow> WatchlistMovies,
    IReadOnlyList<AiWatchedMovieRow> WatchedMovies,
    IReadOnlyList<AiTvGenreRow> WatchedTvGenres);

public sealed record AiRatingRow(
    Guid? MovieId,
    string? MovieTitle,
    int? MovieYear,
    IReadOnlyList<string> MovieGenres,
    int Score);

public sealed record AiMovieTitleRow(
    string Title,
    int? Year,
    IReadOnlyList<string> Genres);

public sealed record AiWatchedMovieRow(
    Guid MovieId,
    string Title,
    int? Year,
    IReadOnlyList<string> Genres);

public sealed record AiTvGenreRow(string Genre, int WatchCount);
