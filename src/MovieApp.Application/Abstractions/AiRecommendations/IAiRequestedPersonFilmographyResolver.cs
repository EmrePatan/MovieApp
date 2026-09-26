using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiRequestedPersonFilmographyResolver
{
    Task<PersonFilmographyConstraint?> TryResolveAsync(
        string userMessage,
        string? searchLanguage = null,
        CancellationToken cancellationToken = default);
}
