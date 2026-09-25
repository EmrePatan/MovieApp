using MovieApp.Application.Models.Library;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ILibraryActionStatusRepository
{
    Task<LibraryActionSnapshot> GetMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<LibraryActionSnapshot> GetTvShowAsync(
        Guid userId,
        Guid tvShowId,
        Guid? episodeId,
        CancellationToken cancellationToken = default);
}
