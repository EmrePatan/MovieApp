using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class PushDevice
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string ExpoPushToken { get; set; } = string.Empty;

    public PushDevicePlatform Platform { get; set; }

    public string? DeviceIdentifier { get; set; }

    public string? ContentLocale { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? LastSeenAtUtc { get; set; }

    public User User { get; set; } = null!;
}
