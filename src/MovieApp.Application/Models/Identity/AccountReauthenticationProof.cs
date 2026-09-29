namespace MovieApp.Application.Models.Identity;

public sealed record AccountReauthenticationProof(
    string? CurrentPassword,
    string? Provider,
    string? IdentityToken);
