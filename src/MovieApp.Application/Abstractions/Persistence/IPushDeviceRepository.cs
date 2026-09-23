using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IPushDeviceRepository
{
    Task<PushDevice?> GetByTokenAsync(string expoPushToken, CancellationToken cancellationToken = default);

    Task<PushDevice?> GetActiveByTokenForUserAsync(
        Guid userId,
        string expoPushToken,
        CancellationToken cancellationToken = default);

    Task<PushDevice> RegisterOrReassignAsync(
        Guid userId,
        string expoPushToken,
        PushDevicePlatform platform,
        string? deviceIdentifier,
        string? contentLocale,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateForUserAsync(
        Guid userId,
        string expoPushToken,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
