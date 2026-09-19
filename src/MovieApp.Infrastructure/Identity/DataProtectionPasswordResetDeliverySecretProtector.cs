using Microsoft.AspNetCore.DataProtection;
using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Infrastructure.Identity;

public sealed class DataProtectionPasswordResetDeliverySecretProtector(
    IDataProtectionProvider dataProtectionProvider) : IPasswordResetDeliverySecretProtector
{
    private const string ProtectorPurpose = "MovieApp.PasswordResetDelivery.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

    public string Protect(string rawToken) =>
        _protector.Protect(rawToken);

    public string Unprotect(string protectedPayload) =>
        _protector.Unprotect(protectedPayload);
}
