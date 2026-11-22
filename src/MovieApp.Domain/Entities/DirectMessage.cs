namespace MovieApp.Domain.Entities;

public sealed class DirectMessage
{
    public const int MaxBodyLength = 2000;

    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public static DirectMessage Create(Guid conversationId, Guid senderId, string body, DateTime utcNow)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (senderId == Guid.Empty)
        {
            throw new ArgumentException("Sender id is required.", nameof(senderId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var normalized = body.Trim();
        if (normalized.Length > MaxBodyLength)
        {
            throw new ArgumentOutOfRangeException(nameof(body), "Message cannot exceed 2000 characters.");
        }

        return new DirectMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Body = normalized,
            CreatedAt = utcNow
        };
    }
}
