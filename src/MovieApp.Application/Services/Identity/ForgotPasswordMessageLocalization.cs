using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Identity;

public static class ForgotPasswordMessageLocalization
{
    public const string EnglishSuccessMessage =
        "If an account exists for this email, you will receive instructions to reset your password.";

    public const string TurkishSuccessMessage =
        "Bu e-posta adresine kayıtlı bir hesap varsa, şifreni sıfırlamak için talimatlar gönderilecektir.";

    public const string SpanishSuccessMessage =
        "Si existe una cuenta asociada a este correo electrónico, recibirás instrucciones para restablecer tu contraseña.";

    public const string GermanSuccessMessage =
        "Wenn ein Konto für diese E-Mail-Adresse existiert, erhältst du Anweisungen zum Zurücksetzen deines Passworts.";

    public const string FrenchSuccessMessage =
        "Si un compte existe pour cette adresse e-mail, vous recevrez des instructions pour réinitialiser votre mot de passe.";

    public const string ItalianSuccessMessage =
        "Se esiste un account associato a questa e-mail, riceverai le istruzioni per reimpostare la password.";

    public const string PortugueseBrazilSuccessMessage =
        "Se existir uma conta para este e-mail, você receberá instruções para redefinir sua senha.";

    public static string GetSuccessMessage(string contentLocale)
    {
        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);
        return normalizedLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => TurkishSuccessMessage,
            ContentLocaleResolver.SpanishSpain => SpanishSuccessMessage,
            ContentLocaleResolver.GermanGermany => GermanSuccessMessage,
            ContentLocaleResolver.FrenchFrance => FrenchSuccessMessage,
            ContentLocaleResolver.ItalianItaly => ItalianSuccessMessage,
            ContentLocaleResolver.PortugueseBrazil => PortugueseBrazilSuccessMessage,
            _ => EnglishSuccessMessage
        };
    }
}
