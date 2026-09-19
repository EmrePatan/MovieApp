namespace MovieApp.Application.Models.Identity;

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email);
