namespace MovieApp.Application.Abstractions.Persistence;

public interface IUserKeywordAffinityRepository
{
    /// <summary>
    /// How many distinct watched titles (movies, plus shows with at least one watched episode)
    /// carry each displayable keyword. Strongest keywords first, capped at <paramref name="maxCount"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetWatchedKeywordCountsAsync(
        Guid userId,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetKeywordNamesAsync(
        IReadOnlyCollection<Guid> keywordIds,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
