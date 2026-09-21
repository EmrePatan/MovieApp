using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.PushDevices;

public sealed class RegisterPushDeviceService(
    ICurrentUser currentUser,
    IPushDeviceRepository pushDeviceRepository,
    ILogger<RegisterPushDeviceService> logger) : IRegisterPushDeviceService
{
    public async Task RegisterAsync(
        string expoPushToken,
        string platform,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        PushDeviceValidator.ValidateRegistration(expoPushToken, platform);

        if (!PushDeviceValidator.TryParsePlatform(platform, out var parsedPlatform))
        {
            throw new Exceptions.ValidationException("Platform must be 'ios' or 'android'.");
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var utcNow = DateTime.UtcNow;

        var persistenceStopwatch = Stopwatch.StartNew();
        await pushDeviceRepository.RegisterOrReassignAsync(
            userId,
            expoPushToken.Trim(),
            parsedPlatform,
            deviceIdentifier: null,
            utcNow,
            cancellationToken);
        persistenceStopwatch.Stop();
        totalStopwatch.Stop();

        PushDevicePerfLogMessages.LogRegister(
            logger,
            totalStopwatch.ElapsedMilliseconds,
            persistenceStopwatch.ElapsedMilliseconds,
            platform);
    }
}
