namespace MovieApp.Application.Caching;

public static class AiRecommendationCacheKeys
{
    public static string TasteProfile(Guid userId) => $"ai:taste:{userId:D}";

    public static string Session(Guid userId, Guid sessionId) => $"ai:session:{userId:D}:{sessionId:D}";
}
