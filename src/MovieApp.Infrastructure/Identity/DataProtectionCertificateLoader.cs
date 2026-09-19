using System.Security.Cryptography.X509Certificates;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Identity;

internal static class DataProtectionCertificateLoader
{
    internal static X509Certificate2 Load(MovieAppDataProtectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            throw new InvalidOperationException("DataProtection:CertificatePath is required.");
        }

        return string.IsNullOrWhiteSpace(options.CertificatePassword)
            ? X509CertificateLoader.LoadPkcs12FromFile(options.CertificatePath, ReadOnlySpan<char>.Empty)
            : X509CertificateLoader.LoadPkcs12FromFile(options.CertificatePath, options.CertificatePassword.AsSpan());
    }
}
