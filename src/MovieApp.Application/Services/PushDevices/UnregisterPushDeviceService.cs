using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.PushDevices;

public sealed class UnregisterPushDeviceService(
    ICurrentUser currentUser,
    IPushDeviceRepository pushDeviceRepository) : IUnregisterPushDeviceService
{
    public async Task UnregisterAsync(
        string expoPushToken,
        CancellationToken cancellationToken = default)
    {
        PushDeviceValidator.ValidateUnregister(expoPushToken);

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await pushDeviceRepository.DeactivateForUserAsync(
            userId,
            expoPushToken.Trim(),
            DateTime.UtcNow,
            cancellationToken);
    }
}
