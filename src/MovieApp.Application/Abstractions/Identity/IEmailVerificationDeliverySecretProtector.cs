namespace MovieApp.Application.Abstractions.Identity;

public interface IEmailVerificationDeliverySecretProtector
{
    string Protect(string rawToken);

    string Unprotect(string protectedPayload);
}
