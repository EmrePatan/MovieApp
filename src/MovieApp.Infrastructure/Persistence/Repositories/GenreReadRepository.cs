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

    public async Task<Guid?> GetIdByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return await dbContext.Genres
            .AsNoTracking()
            .Where(genre => EF.Functions.ILike(genre.Name, name))
            .Select(genre => (Guid?)genre.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByMovieIdsAsync(
        IReadOnlyList<Guid> movieIds,
        int maxGenresPerItem,
        CancellationToken cancellationToken = default)
    {
        if (movieIds.Count == 0 || maxGenresPerItem <= 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var rows = await dbContext.MovieGenres
            .AsNoTracking()
            .Where(join => movieIds.Contains(join.MovieId))
            .Select(join => new { join.MovieId, GenreName = join.Genre.Name })
            .ToListAsync(cancellationToken);

        return GroupOrderedGenreNames(rows, row => row.MovieId, row => row.GenreName, maxGenresPerItem);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByTvShowIdsAsync(
        IReadOnlyList<Guid> tvShowIds,
        int maxGenresPerItem,
        CancellationToken cancellationToken = default)
    {
        if (tvShowIds.Count == 0 || maxGenresPerItem <= 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var rows = await dbContext.TvShowGenres
            .AsNoTracking()
            .Where(join => tvShowIds.Contains(join.TvShowId))
            .Select(join => new { join.TvShowId, GenreName = join.Genre.Name })
            .ToListAsync(cancellationToken);

        return GroupOrderedGenreNames(rows, row => row.TvShowId, row => row.GenreName, maxGenresPerItem);
    }

    private static Dictionary<Guid, IReadOnlyList<string>> GroupOrderedGenreNames<T>(
        IReadOnlyList<T> rows,
        Func<T, Guid> idSelector,
        Func<T, string> nameSelector,
        int maxGenresPerItem) =>
        rows
            .GroupBy(idSelector)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(nameSelector)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .Take(maxGenresPerItem)
                    .ToList());
}
