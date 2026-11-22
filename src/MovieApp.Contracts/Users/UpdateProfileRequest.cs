namespace MovieApp.Contracts.Users;

public sealed record UpdateProfileRequest(
    string DisplayName,
    string? Bio = null,
    ProfileSocialLinksDto? SocialLinks = null);
