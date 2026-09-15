using MovieApp.Domain.Users;

namespace MovieApp.Domain.Entities;

public sealed class UserExternalLogin
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ProviderSubject { get; set; } = string.Empty;

    public string? EmailAtLinkTime { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    public static UserExternalLogin Create(
        Guid id,
        Guid userId,
        string provider,
        string providerSubject,
        string? emailAtLinkTime,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubject);

        var normalizedProvider = ExternalLoginProviders.Normalize(provider);

        return new UserExternalLogin
        {
            Id = id,
            UserId = userId,
            Provider = normalizedProvider,
            ProviderSubject = providerSubject.Trim(),
            EmailAtLinkTime = string.IsNullOrWhiteSpace(emailAtLinkTime) ? null : emailAtLinkTime.Trim(),
            CreatedAt = utcNow,
        };
    }
}
