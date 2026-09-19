using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Identity;

public static class ForgotPasswordMessageLocalization
{
    public const string EnglishSuccessMessage =
        "If an account exists for this email, you will receive instructions to reset your password.";

    public const string TurkishSuccessMessage =
        "Bu e-posta adresine kayıtlı bir hesap varsa, şifreni sıfırlamak için talimatlar gönderilecektir.";

    public static string GetSuccessMessage(string contentLocale) =>
        ContentLocaleResolver.RequiresLocalization(contentLocale)
            ? TurkishSuccessMessage
            : EnglishSuccessMessage;
}
