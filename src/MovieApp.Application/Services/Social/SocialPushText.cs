using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Social;

/// <summary>Localized push body for each social notification type ("{actor}" is the push title).</summary>
public static class SocialPushText
{
    public const string ActivityLike = "ActivityLike";
    public const string CommentLike = "CommentLike";
    public const string NewComment = "NewComment";
    public const string CommentReply = "CommentReply";
    public const string NewFollower = "NewFollower";
    public const string FollowRequest = "FollowRequest";
    public const string FollowAccepted = "FollowAccepted";

    public static string? Compose(string notificationType, string? deviceLocale)
    {
        var locale = string.IsNullOrWhiteSpace(deviceLocale)
            ? ContentLocaleResolver.EnglishUnitedStates
            : ContentLocaleResolver.Normalize(deviceLocale);

        return notificationType switch
        {
            ActivityLike => Pick(locale, "liked your post", "paylaşımını beğendi", "le gustó tu publicación",
                "gefällt dein Beitrag", "a aimé votre publication", "ha messo mi piace al tuo post",
                "curtiu sua publicação"),
            CommentLike => Pick(locale, "liked your comment", "yorumunu beğendi", "le gustó tu comentario",
                "gefällt dein Kommentar", "a aimé votre commentaire", "ha messo mi piace al tuo commento",
                "curtiu seu comentário"),
            NewComment => Pick(locale, "commented on your post", "paylaşımına yorum yaptı",
                "comentó tu publicación", "hat deinen Beitrag kommentiert", "a commenté votre publication",
                "ha commentato il tuo post", "comentou na sua publicação"),
            CommentReply => Pick(locale, "replied to your comment", "yorumuna yanıt verdi",
                "respondió a tu comentario", "hat auf deinen Kommentar geantwortet",
                "a répondu à votre commentaire", "ha risposto al tuo commento", "respondeu ao seu comentário"),
            NewFollower => Pick(locale, "started following you", "seni takip etmeye başladı",
                "empezó a seguirte", "folgt dir jetzt", "a commencé à vous suivre", "ha iniziato a seguirti",
                "começou a seguir você"),
            FollowRequest => Pick(locale, "wants to follow you", "seni takip etmek istiyor",
                "quiere seguirte", "möchte dir folgen", "souhaite vous suivre", "vuole seguirti",
                "quer seguir você"),
            FollowAccepted => Pick(locale, "accepted your follow request", "takip isteğini kabul etti",
                "aceptó tu solicitud de seguimiento", "hat deine Folgeanfrage angenommen",
                "a accepté votre demande d'abonnement", "ha accettato la tua richiesta di follow",
                "aceitou seu pedido para seguir"),
            _ => null,
        };
    }

    private static string Pick(
        string locale, string en, string tr, string es, string de, string fr, string it, string pt) =>
        locale switch
        {
            ContentLocaleResolver.TurkishTurkey => tr,
            ContentLocaleResolver.SpanishSpain => es,
            ContentLocaleResolver.GermanGermany => de,
            ContentLocaleResolver.FrenchFrance => fr,
            ContentLocaleResolver.ItalianItaly => it,
            ContentLocaleResolver.PortugueseBrazil => pt,
            _ => en,
        };
}
