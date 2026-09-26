namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiRecommendationCatalogTitleAliasReader
{
    Task<IReadOnlyList<string>> GetTitleAliasesAsync(
        string mediaType,
        Guid contentId,
        CancellationToken cancellationToken = default);
}
