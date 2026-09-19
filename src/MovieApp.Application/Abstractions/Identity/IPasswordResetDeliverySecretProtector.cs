namespace MovieApp.Application.Abstractions.Identity;

public interface IPasswordResetDeliverySecretProtector
{
    string Protect(string rawToken);

    string Unprotect(string protectedPayload);
}
