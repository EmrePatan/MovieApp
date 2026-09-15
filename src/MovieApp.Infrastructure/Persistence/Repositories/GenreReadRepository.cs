using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class GenreReadRepository(ApplicationDbContext dbContext) : IGenreReadRepository
{
    public async Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Genres
            .AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Select(genre => new ValueTuple<Guid, string>(genre.Id, genre.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IReadOnlyList<Guid> genreIds,
        CancellationToken cancellationToken = default)
    {
        if (genreIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Genres
            .AsNoTracking()
            .Where(genre => genreIds.Contains(genre.Id))
            .ToDictionaryAsync(genre => genre.Id, genre => genre.Name, cancellationToken);
    }
}
