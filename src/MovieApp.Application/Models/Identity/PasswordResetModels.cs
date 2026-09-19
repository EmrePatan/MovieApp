using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Models.Identity;

public sealed record ForgotPasswordRequest(
    string Email,
    string ContentLocale = ContentLocaleResolver.EnglishUnitedStates);

public sealed record PasswordResetDeliveryTarget(
    Guid TokenId,
    Guid UserId,
    string Email,
    string ProtectedDeliverySecret,
    bool IsDeliverable,
    string ContentLocale);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record MessageResult(string Message);
