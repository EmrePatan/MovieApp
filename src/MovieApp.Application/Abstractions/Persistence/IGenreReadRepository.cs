namespace MovieApp.Application.Abstractions.Persistence;

public interface IGenreReadRepository
{
    Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IReadOnlyList<Guid> genreIds,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetIdByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByMovieIdsAsync(
        IReadOnlyList<Guid> movieIds,
        int maxGenresPerItem,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByTvShowIdsAsync(
        IReadOnlyList<Guid> tvShowIds,
        int maxGenresPerItem,
        CancellationToken cancellationToken = default);
}
