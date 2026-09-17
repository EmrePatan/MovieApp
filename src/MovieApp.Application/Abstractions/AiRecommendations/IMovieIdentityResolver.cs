using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IMovieIdentityResolver
{
    Task<ResolvedMovieIdentity?> ResolveAsync(
        AiProviderSuggestion suggestion,
        CancellationToken cancellationToken = default);
}
