namespace MovieApp.Domain.Entities;

public sealed class DirectConversationParticipant
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    public DateTime? LastReadAt { get; set; }

    /// <summary>
    /// When this participant removed the conversation from their own inbox. Messages at or before
    /// this instant are hidden for this participant only; the peer's view is unaffected.
    /// </summary>
    public DateTime? HistoryClearedAt { get; set; }

    public static DirectConversationParticipant Create(Guid conversationId, Guid userId)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return new DirectConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            UserId = userId
        };
    }

    public void ClearHistory(DateTime utcNow)
    {
        if (HistoryClearedAt is null || HistoryClearedAt < utcNow)
        {
            HistoryClearedAt = utcNow;
        }
    }

    public void MarkRead(DateTime readThrough)
    {
        if (LastReadAt is null || LastReadAt < readThrough)
        {
            LastReadAt = readThrough;
        }
    }
}
