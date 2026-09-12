namespace MovieApp.Application.Models.Identity;

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record MessageResult(string Message);
