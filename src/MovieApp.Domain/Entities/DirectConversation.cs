namespace MovieApp.Domain.Entities;

public sealed class DirectConversation
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public static DirectConversation Create(DateTime utcNow)
    {
        return new DirectConversation
        {
            Id = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void Touch(DateTime utcNow)
    {
        UpdatedAt = utcNow;
        LastMessageAt = utcNow;
    }
}
