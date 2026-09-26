using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IMovieIdentityResolver
{
    Task<ResolvedMovieIdentity?> ResolveAsync(
        AiProviderSuggestion suggestion,
        string? searchLanguage = null,
        CancellationToken cancellationToken = default);
}
