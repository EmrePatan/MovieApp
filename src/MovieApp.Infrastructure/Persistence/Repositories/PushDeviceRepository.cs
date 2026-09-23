using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class PushDeviceRepository(ApplicationDbContext dbContext) : IPushDeviceRepository
{
    public Task<PushDevice?> GetByTokenAsync(
        string expoPushToken,
        CancellationToken cancellationToken = default) =>
        dbContext.PushDevices
            .FirstOrDefaultAsync(device => device.ExpoPushToken == expoPushToken, cancellationToken);

    public Task<PushDevice?> GetActiveByTokenForUserAsync(
        Guid userId,
        string expoPushToken,
        CancellationToken cancellationToken = default) =>
        dbContext.PushDevices
            .FirstOrDefaultAsync(
                device =>
                    device.UserId == userId &&
                    device.ExpoPushToken == expoPushToken &&
                    device.IsActive,
                cancellationToken);

    public async Task<PushDevice> RegisterOrReassignAsync(
        Guid userId,
        string expoPushToken,
        PushDevicePlatform platform,
        string? deviceIdentifier,
        string? contentLocale,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByTokenAsync(expoPushToken, cancellationToken);

        if (existing is null)
        {
            var device = new PushDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExpoPushToken = expoPushToken,
                Platform = platform,
                DeviceIdentifier = deviceIdentifier,
                ContentLocale = contentLocale,
                IsActive = true,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow,
                LastSeenAtUtc = utcNow
            };

            dbContext.PushDevices.Add(device);
            await dbContext.SaveChangesAsync(cancellationToken);
            return device;
        }

        existing.UserId = userId;
        existing.Platform = platform;
        existing.DeviceIdentifier = deviceIdentifier;
        existing.ContentLocale = contentLocale;
        existing.IsActive = true;
        existing.UpdatedAtUtc = utcNow;
        existing.LastSeenAtUtc = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeactivateForUserAsync(
        Guid userId,
        string expoPushToken,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var device = await dbContext.PushDevices
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.ExpoPushToken == expoPushToken &&
                    candidate.UserId == userId,
                cancellationToken);

        if (device is null || !device.IsActive)
        {
            return false;
        }

        device.IsActive = false;
        device.UpdatedAtUtc = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
