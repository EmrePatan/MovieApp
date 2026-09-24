using System.Runtime.InteropServices;
using System.Security;

namespace MovieApp.LoadTestIdentityProvisioner;

public static class SecureStringUtilities
{
    public static string ToPlainString(SecureString secure)
    {
        ArgumentNullException.ThrowIfNull(secure);
        var bstr = Marshal.SecureStringToBSTR(secure);
        try
        {
            return Marshal.PtrToStringBSTR(bstr) ?? string.Empty;
        }
        finally
        {
            Marshal.ZeroFreeBSTR(bstr);
        }
    }

    /// <summary>For unit tests only — never log the returned secure string's content.</summary>
    internal static SecureString CreateFromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var secure = new SecureString();
        foreach (var ch in value)
        {
            secure.AppendChar(ch);
        }

        secure.MakeReadOnly();
        return secure;
    }
}
