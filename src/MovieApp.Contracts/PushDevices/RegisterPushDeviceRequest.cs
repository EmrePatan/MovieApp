namespace MovieApp.Contracts.PushDevices;

public sealed record RegisterPushDeviceRequest(
    string ExpoPushToken,
    string Platform);
