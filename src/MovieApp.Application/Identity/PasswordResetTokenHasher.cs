using System.Security.Cryptography;
using System.Text;

namespace MovieApp.Application.Identity;

public static class PasswordResetTokenHasher
{
    public static string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
