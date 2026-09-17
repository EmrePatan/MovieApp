using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiRecommendationQuotaService
{
    Task<AiQuotaReservation> CheckAndReserveAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task CommitAsync(
        Guid userId,
        AiQuotaReservation reservation,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        Guid userId,
        AiQuotaReservation reservation,
        CancellationToken cancellationToken = default);

    Task<int> GetRemainingUserQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
