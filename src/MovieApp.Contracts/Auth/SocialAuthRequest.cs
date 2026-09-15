namespace MovieApp.Contracts.Auth;

public sealed record SocialAuthRequest(string Provider, string IdentityToken);
