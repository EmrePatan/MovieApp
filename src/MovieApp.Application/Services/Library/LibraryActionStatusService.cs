using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Library;

namespace MovieApp.Application.Services.Library;

public sealed class LibraryActionStatusService(
    ICurrentUser currentUser,
    ILibraryActionStatusRepository libraryActionStatusRepository) : ILibraryActionStatusService
{
    public async Task<LibraryActionSnapshot> GetAsync(
        string? mediaType,
        Guid contentId,
        Guid? episodeId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (contentId == Guid.Empty)
        {
            throw new ValidationException("Content id is required.");
        }

        if (string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            if (episodeId is not null)
            {
                throw new ValidationException("Episode id applies only to TV shows.");
            }

            return await libraryActionStatusRepository.GetMovieAsync(userId, contentId, cancellationToken);
        }

        if (string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            return await libraryActionStatusRepository.GetTvShowAsync(
                userId,
                contentId,
                episodeId,
                cancellationToken);
        }

        throw new ValidationException("Media type must be 'movie' or 'tv'.");
    }
}
