namespace MovieApp.Application.Models.Identity;

public sealed record VerifiedSocialIdentity(
    string Provider,
    string Subject,
    string? Email,
    bool IsEmailVerified,
    string? DisplayName);
