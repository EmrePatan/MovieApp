using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Search;

public sealed class GenreService(IGenreReadRepository genreReadRepository) : IGenreService
{
    public async Task<IReadOnlyList<(Guid Id, string Name)>> GetAllAsync(
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var genres = await genreReadRepository.GetAllOrderedByNameAsync(cancellationToken);

        return genres
            .Select(genre => (genre.Id, GenreLocalization.Localize(genre.Name, contentLocale)))
            .ToList();
    }
}
