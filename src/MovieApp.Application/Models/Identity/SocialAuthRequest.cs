namespace MovieApp.Application.Models.Identity;

public sealed record SocialAuthRequest(string Provider, string IdentityToken);
