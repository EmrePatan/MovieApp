using MovieApp.Application.Models.Library;

namespace MovieApp.Application.Services.Library;

public interface ILibraryActionStatusService
{
    Task<LibraryActionSnapshot> GetAsync(
        string? mediaType,
        Guid contentId,
        Guid? episodeId,
        CancellationToken cancellationToken = default);
}
