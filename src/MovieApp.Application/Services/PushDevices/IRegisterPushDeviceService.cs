namespace MovieApp.Application.Services.PushDevices;

public interface IRegisterPushDeviceService
{
    Task RegisterAsync(
        string expoPushToken,
        string platform,
        CancellationToken cancellationToken = default);
}
