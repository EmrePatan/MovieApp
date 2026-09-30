namespace MovieApp.Application.Models.Identity;

public sealed record UserAvatarSources(
    Guid UserId,
    string? CustomAvatarStorageKey,
    string? GoogleProviderPictureUrl);
