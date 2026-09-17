using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiTasteProfileBuilder
{
    Task<AiTasteProfile> BuildAsync(Guid userId, CancellationToken cancellationToken = default);
}
