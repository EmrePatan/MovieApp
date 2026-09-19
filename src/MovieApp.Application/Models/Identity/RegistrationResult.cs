namespace MovieApp.Application.Models.Identity;

public sealed record RegistrationResult(
    CurrentUserResult User,
    bool RequiresEmailVerification,
    string Message);
