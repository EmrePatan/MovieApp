namespace MovieApp.LoadTestIdentityProvisioner;

internal static class SecurePasswordPrompt
{
    public static string ReadCampaignPassword(string prompt)
    {
        Console.Write(prompt);
        var secure = ReadSecureLine();
        try
        {
            var plain = SecureStringUtilities.ToPlainString(secure);
            if (string.IsNullOrWhiteSpace(plain))
            {
                throw new InvalidOperationException("Password cannot be empty.");
            }

            return plain;
        }
        finally
        {
            secure.Dispose();
        }
    }

    private static System.Security.SecureString ReadSecureLine()
    {
        var secure = new System.Security.SecureString();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (secure.Length > 0)
                {
                    secure.RemoveAt(secure.Length - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                secure.AppendChar(key.KeyChar);
            }
        }

        return secure;
    }

}
