namespace MovieApp.Application.Services.PushDevices;

public interface IUnregisterPushDeviceService
{
    Task UnregisterAsync(
        string expoPushToken,
        CancellationToken cancellationToken = default);
}
