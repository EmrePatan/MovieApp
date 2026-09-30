namespace MovieApp.Application.Models.Identity;

public sealed record UserAvatarPresentation(
    string? CustomAvatarUrl,
    string? ProviderAvatarUrl,
    string? EffectiveAvatarUrl,
    UserAvatarKind AvatarKind);
