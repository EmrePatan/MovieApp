namespace MovieApp.Domain.Entities;

public sealed class SocialActivityLike
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
