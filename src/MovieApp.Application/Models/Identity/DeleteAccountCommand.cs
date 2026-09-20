namespace MovieApp.Application.Models.Identity;

public sealed record DeleteAccountCommand(
    string? CurrentPassword,
    string? Provider,
    string? IdentityToken);
