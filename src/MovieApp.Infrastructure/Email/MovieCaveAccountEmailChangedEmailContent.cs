using MovieApp.Application.Services.Localization;

namespace MovieApp.Infrastructure.Email;

public static class MovieCaveAccountEmailChangedEmailContent
{
    public static string GetSubject(string contentLocale) => GetCopy(contentLocale).Subject;

    public static string BuildPlainText(string contentLocale) =>
        $"""
        MOVIE CAVE

        {GetCopy(contentLocale).Body}

        Movie Cave
        """;

    private static Copy GetCopy(string contentLocale)
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage(contentLocale);
        return locale switch
        {
            "tr-TR" => new Copy(
                "Movie Cave hesap e-postası değiştirildi",
                "Movie Cave hesabınızın e-posta adresi değiştirildi. Bu değişikliği siz yaptıysanız herhangi bir işlem gerekmez. Siz yapmadıysanız hesabınızı güvence altına almak için destek ile iletişime geçin."),
            "de-DE" => new Copy(
                "Movie-Cave-Konto-E-Mail wurde geändert",
                "Die E-Mail-Adresse deines Movie-Cave-Kontos wurde geändert. Wenn du das warst, ist keine Aktion nötig. Wenn nicht, wende dich an den Support, um dein Konto zu sichern."),
            "es-ES" => new Copy(
                "Se cambió el correo de tu cuenta de Movie Cave",
                "Se cambió la dirección de correo de tu cuenta de Movie Cave. Si fuiste tú, no necesitas hacer nada. Si no, contacta con soporte para proteger tu cuenta."),
            "fr-FR" => new Copy(
                "L’e-mail de votre compte Movie Cave a été modifié",
                "L’adresse e-mail de votre compte Movie Cave a été modifiée. Si c’était vous, aucune action n’est requise. Sinon, contactez l’assistance pour sécuriser votre compte."),
            "it-IT" => new Copy(
                "L’e-mail del tuo account Movie Cave è stata modificata",
                "L’indirizzo e-mail del tuo account Movie Cave è stato modificato. Se sei stato tu, non serve alcuna azione. In caso contrario, contatta l’assistenza per proteggere il tuo account."),
            "pt-PT" => new Copy(
                "O e-mail da sua conta Movie Cave foi alterado",
                "O endereço de e-mail da sua conta Movie Cave foi alterado. Se foi você, nenhuma ação é necessária. Caso contrário, contacte o suporte para proteger a sua conta."),
            _ => new Copy(
                "Your Movie Cave account email was changed",
                "The email address on your Movie Cave account was changed. If this was you, no action is needed. If not, contact support to secure your account."),
        };
    }

    private sealed record Copy(string Subject, string Body);
}
