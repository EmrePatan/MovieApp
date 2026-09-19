namespace MovieApp.Application.Models.Identity;

public sealed record EmailVerificationDeliveryTarget(
    Guid TokenId,
    Guid UserId,
    string Email,
    string ProtectedDeliverySecret,
    bool IsDeliverable,
    string ContentLocale);
