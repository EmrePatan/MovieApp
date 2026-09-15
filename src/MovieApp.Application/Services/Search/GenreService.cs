using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.Application.Services.Search;

public sealed class GenreService(IGenreReadRepository genreReadRepository) : IGenreService
{
    public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        genreReadRepository.GetAllOrderedByNameAsync(cancellationToken);
}
