namespace MovieApp.Application.Services.Localization;

public static class AchievementMilestoneLocalization
{
    private sealed record MilestoneCopy(
        string TitleEn,
        string? DescriptionEn,
        string TitleTr,
        string? DescriptionTr,
        string TitleEs,
        string? DescriptionEs);

    private static readonly Dictionary<string, MilestoneCopy> CopyById =
        new(StringComparer.Ordinal)
        {
            ["first-movie"] = new(
                "First movie watched",
                "You started your movie journey.",
                "İlk film izlendi",
                "Film yolculuğuna başladın.",
                "Primera película vista",
                "Has comenzado tu viaje cinematográfico."),
            ["movies-10"] = new(
                "10 movies watched",
                "A solid start to your catalog.",
                "10 film izlendi",
                "Kataloğuna sağlam bir başlangıç yaptın.",
                "10 películas vistas",
                "Un buen comienzo para tu catálogo."),
            ["movies-50"] = new(
                "50 movies watched",
                "You are building a serious watch history.",
                "50 film izlendi",
                "Ciddi bir izleme geçmişi oluşturuyorsun.",
                "50 películas vistas",
                "Estás construyendo un historial de visionado serio."),
            ["episodes-100"] = new(
                "100 episodes watched",
                "Your series habit is real.",
                "100 bölüm izlendi",
                "Dizi alışkanlığın gerçek.",
                "100 episodios vistos",
                "Tu hábito de series es real."),
            ["episodes-500"] = new(
                "500 episodes watched",
                "A major binge milestone.",
                "500 bölüm izlendi",
                "Büyük bir maraton kilometre taşı.",
                "500 episodios vistos",
                "Un hito importante de maratón."),
            ["ratings-10"] = new(
                "10 ratings",
                "You are shaping your taste profile.",
                "10 puan",
                "Zevk profilini şekillendiriyorsun.",
                "10 valoraciones",
                "Estás definiendo tu perfil de gustos."),
            ["ratings-25"] = new(
                "25 ratings",
                null,
                "25 puan",
                null,
                "25 valoraciones",
                null),
            ["ratings-50"] = new(
                "50 ratings",
                "Your rating voice is well established.",
                "50 puan",
                "Puanlama tarzın oturdu.",
                "50 valoraciones",
                "Tu estilo de valoración está bien definido."),
            ["first-show-completed"] = new(
                "First series completed",
                "You finished every episode of a show.",
                "İlk dizi tamamlandı",
                "Bir dizinin tüm bölümlerini bitirdin.",
                "Primera serie completada",
                "Has terminado todos los episodios de una serie."),
            ["shows-completed-3"] = new(
                "3 series completed",
                null,
                "3 dizi tamamlandı",
                null,
                "3 series completadas",
                null),
            ["genres-5"] = new(
                "5 genres encountered",
                null,
                "5 tür keşfedildi",
                null,
                "5 géneros descubiertos",
                null),
        };

    public static string GetTitle(string milestoneId, string contentLocale, string fallbackTitle)
    {
        if (!CopyById.TryGetValue(milestoneId, out var copy))
        {
            return fallbackTitle;
        }

        return ResolveLocalized(contentLocale, copy.TitleEn, copy.TitleTr, copy.TitleEs, fallbackTitle);
    }

    public static string GetDescription(
        string milestoneId,
        string contentLocale,
        string fallbackDescription)
    {
        if (!CopyById.TryGetValue(milestoneId, out var copy))
        {
            return fallbackDescription;
        }

        var english = copy.DescriptionEn ?? fallbackDescription;
        var turkish = copy.DescriptionTr ?? fallbackDescription;
        var spanish = copy.DescriptionEs ?? fallbackDescription;

        return ResolveLocalized(contentLocale, english, turkish, spanish, fallbackDescription);
    }

    private static string ResolveLocalized(
        string contentLocale,
        string english,
        string turkish,
        string spanish,
        string fallback)
    {
        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);
        return normalizedLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => turkish,
            ContentLocaleResolver.SpanishSpain => spanish,
            _ => string.IsNullOrWhiteSpace(english) ? fallback : english
        };
    }
}
