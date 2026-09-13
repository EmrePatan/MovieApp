namespace MovieApp.Application.Validation;

public static class WatchHistoryBulkValidator
{
    public const int MaxEpisodeIdsPerRequest = 200;

    public static SearchQueryValidationResult ValidateEpisodeIds(IReadOnlyList<Guid> episodeIds)
    {
        if (episodeIds is null || episodeIds.Count == 0)
        {
            return SearchQueryValidationResult.Failure("At least one episode ID is required.");
        }

        if (episodeIds.Count > MaxEpisodeIdsPerRequest)
        {
            return SearchQueryValidationResult.Failure(
                $"A maximum of {MaxEpisodeIdsPerRequest} episode IDs can be updated per request.");
        }

        return SearchQueryValidationResult.Success();
    }
}
