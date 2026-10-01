namespace MovieApp.UnitTests.Persistence;

internal static class GenreReadRepositoryTestDefaults
{
    public static Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> EmptyMovieGenresAsync(
        IReadOnlyList<Guid> movieIds,
        int maxGenresPerItem,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(
            new Dictionary<Guid, IReadOnlyList<string>>());

    public static Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> EmptyTvGenresAsync(
        IReadOnlyList<Guid> tvShowIds,
        int maxGenresPerItem,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(
            new Dictionary<Guid, IReadOnlyList<string>>());
}
