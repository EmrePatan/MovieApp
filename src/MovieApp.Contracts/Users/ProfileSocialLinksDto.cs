namespace MovieApp.Contracts.Users;

public sealed record ProfileSocialLinksDto(
    string? X = null,
    string? Instagram = null,
    string? Snapchat = null,
    string? Tiktok = null,
    string? Youtube = null,
    string? Twitch = null,
    string? Threads = null,
    string? Website = null);
