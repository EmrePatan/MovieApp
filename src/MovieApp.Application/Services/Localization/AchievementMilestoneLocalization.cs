namespace MovieApp.Application.Services.Localization;

public static class AchievementMilestoneLocalization
{
    private sealed record MilestoneCopy(
        string TitleEn,
        string? DescriptionEn,
        string TitleTr,
        string? DescriptionTr,
        string TitleEs,
        string? DescriptionEs,
        string TitleDe,
        string? DescriptionDe,
        string TitleFr,
        string? DescriptionFr,
        string TitleIt,
        string? DescriptionIt,
        string TitlePtBr,
        string? DescriptionPtBr);

    private static readonly Dictionary<string, MilestoneCopy> CopyById =
        new(StringComparer.Ordinal)
        {
            ["first-movie"] = new(
                "First movie watched", "You started your movie journey.",
                "İlk film izlendi", "Film yolculuğuna başladın.",
                "Primera película vista", "Has comenzado tu viaje cinematográfico.",
                "Erster Film gesehen", "Du hast deine Filmreise begonnen.",
                "Premier film regardé", "Vous avez commencé votre parcours cinéma.",
                "Primo film guardato", "Hai iniziato il tuo viaggio nel cinema.",
                "Primeiro filme assistido", "Você começou sua jornada no cinema."),
            ["movies-10"] = new(
                "10 movies watched", "A solid start to your catalog.",
                "10 film izlendi", "Kataloğuna sağlam bir başlangıç yaptın.",
                "10 películas vistas", "Un buen comienzo para tu catálogo.",
                "10 Filme gesehen", "Ein solider Start für deine Sammlung.",
                "10 films regardés", "Un bon début pour votre catalogue.",
                "10 film guardati", "Un ottimo inizio per il tuo catalogo.",
                "10 filmes assistidos", "Um começo sólido para o seu catálogo."),
            ["movies-50"] = new(
                "50 movies watched", "You are building a serious watch history.",
                "50 film izlendi", "Ciddi bir izleme geçmişi oluşturuyorsun.",
                "50 películas vistas", "Estás construyendo un historial de visionado serio.",
                "50 Filme gesehen", "Du baust eine ernsthafte Filmhistorie auf.",
                "50 films regardés", "Vous construisez un historique de visionnage solide.",
                "50 film guardati", "Stai costruendo una cronologia di visione seria.",
                "50 filmes assistidos", "Você está construindo um histórico sério de filmes."),
            ["episodes-100"] = new(
                "100 episodes watched", "Your series habit is real.",
                "100 bölüm izlendi", "Dizi alışkanlığın gerçek.",
                "100 episodios vistos", "Tu hábito de series es real.",
                "100 Folgen gesehen", "Deine Seriengewohnheit ist echt.",
                "100 épisodes regardés", "Votre habitude des séries est bien réelle.",
                "100 episodi guardati", "La tua abitudine alle serie è reale.",
                "100 episódios assistidos", "Seu hábito de séries é real."),
            ["episodes-500"] = new(
                "500 episodes watched", "A major binge milestone.",
                "500 bölüm izlendi", "Büyük bir maraton kilometre taşı.",
                "500 episodios vistos", "Un hito importante de maratón.",
                "500 Folgen gesehen", "Ein großer Binge-Meilenstein.",
                "500 épisodes regardés", "Une étape majeure de binge-watching.",
                "500 episodi guardati", "Un traguardo importante di maratona.",
                "500 episódios assistidos", "Um marco importante de maratona."),
            ["ratings-10"] = new(
                "10 ratings", "You are shaping your taste profile.",
                "10 puan", "Zevk profilini şekillendiriyorsun.",
                "10 valoraciones", "Estás definiendo tu perfil de gustos.",
                "10 Bewertungen", "Du formst dein Geschmacksprofil.",
                "10 notes", "Vous façonnez votre profil de goûts.",
                "10 valutazioni", "Stai definendo il tuo profilo di gusti.",
                "10 avaliações", "Você está moldando seu perfil de gostos."),
            ["ratings-25"] = new(
                "25 ratings", null,
                "25 puan", null,
                "25 valoraciones", null,
                "25 Bewertungen", null,
                "25 notes", null,
                "25 valutazioni", null,
                "25 avaliações", null),
            ["ratings-50"] = new(
                "50 ratings", "Your rating voice is well established.",
                "50 puan", "Puanlama tarzın oturdu.",
                "50 valoraciones", "Tu estilo de valoración está bien definido.",
                "50 Bewertungen", "Dein Bewertungsstil ist etabliert.",
                "50 notes", "Votre style de notation est bien établi.",
                "50 valutazioni", "Il tuo stile di valutazione è consolidato.",
                "50 avaliações", "Seu estilo de avaliação está bem definido."),
            ["first-show-completed"] = new(
                "First series completed", "You finished every episode of a show.",
                "İlk dizi tamamlandı", "Bir dizinin tüm bölümlerini bitirdin.",
                "Primera serie completada", "Has terminado todos los episodios de una serie.",
                "Erste Serie abgeschlossen", "Du hast jede Folge einer Serie gesehen.",
                "Première série terminée", "Vous avez terminé tous les épisodes d'une série.",
                "Prima serie completata", "Hai finito tutti gli episodi di una serie.",
                "Primeira série concluída", "Você terminou todos os episódios de uma série."),
            ["shows-completed-3"] = new(
                "3 series completed", null,
                "3 dizi tamamlandı", null,
                "3 series completadas", null,
                "3 Serien abgeschlossen", null,
                "3 séries terminées", null,
                "3 serie completate", null,
                "3 séries concluídas", null),
            ["genres-5"] = new(
                "5 genres encountered", null,
                "5 tür keşfedildi", null,
                "5 géneros descubiertos", null,
                "5 Genres entdeckt", null,
                "5 genres découverts", null,
                "5 generi scoperti", null,
                "5 gêneros descobertos", null),
        };

    public static string GetTitle(string milestoneId, string contentLocale, string fallbackTitle)
    {
        if (!CopyById.TryGetValue(milestoneId, out var copy))
        {
            return fallbackTitle;
        }

        return ResolveTitle(contentLocale, copy, fallbackTitle);
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

        return ResolveDescription(contentLocale, copy, fallbackDescription);
    }

    private static string ResolveTitle(string contentLocale, MilestoneCopy copy, string fallback)
    {
        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);
        var title = normalizedLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => copy.TitleTr,
            ContentLocaleResolver.SpanishSpain => copy.TitleEs,
            ContentLocaleResolver.GermanGermany => copy.TitleDe,
            ContentLocaleResolver.FrenchFrance => copy.TitleFr,
            ContentLocaleResolver.ItalianItaly => copy.TitleIt,
            ContentLocaleResolver.PortugueseBrazil => copy.TitlePtBr,
            _ => copy.TitleEn,
        };

        return string.IsNullOrWhiteSpace(title) ? fallback : title;
    }

    private static string ResolveDescription(string contentLocale, MilestoneCopy copy, string fallback)
    {
        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);
        var description = normalizedLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => copy.DescriptionTr,
            ContentLocaleResolver.SpanishSpain => copy.DescriptionEs,
            ContentLocaleResolver.GermanGermany => copy.DescriptionDe,
            ContentLocaleResolver.FrenchFrance => copy.DescriptionFr,
            ContentLocaleResolver.ItalianItaly => copy.DescriptionIt,
            ContentLocaleResolver.PortugueseBrazil => copy.DescriptionPtBr,
            _ => copy.DescriptionEn,
        };

        return string.IsNullOrWhiteSpace(description) ? fallback : description;
    }
}
