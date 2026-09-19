namespace MovieApp.Contracts.Auth;

public sealed record RegisterResponse(
    string Email,
    bool RequiresEmailVerification,
    string Message);

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email);
