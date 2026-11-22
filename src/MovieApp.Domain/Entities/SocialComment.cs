namespace MovieApp.Domain.Entities;

public sealed class SocialComment
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AuthoringLocale { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public static SocialComment Create(
        Guid activityId,
        Guid authorId,
        Guid? parentCommentId,
        string content,
        string? authoringLocale,
        DateTime utcNow)
    {
        if (activityId == Guid.Empty) throw new ArgumentException("Activity id is required.", nameof(activityId));
        if (authorId == Guid.Empty) throw new ArgumentException("Author id is required.", nameof(authorId));
        if (parentCommentId == Guid.Empty) throw new ArgumentException("Parent comment id cannot be empty.", nameof(parentCommentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var normalized = content.Trim();
        if (normalized.Length > 4000) throw new ArgumentOutOfRangeException(nameof(content), "Comment cannot exceed 4000 characters.");
        return new SocialComment
        {
            Id = Guid.NewGuid(), ActivityId = activityId, AuthorId = authorId,
            ParentCommentId = parentCommentId,
            Content = normalized,
            AuthoringLocale = authoringLocale,
            CreatedAt = utcNow,
        };
    }

    public void Edit(Guid editorId, string content, string? authoringLocale, DateTime utcNow)
    {
        if (editorId != AuthorId) throw new InvalidOperationException("Only the comment author can edit it.");
        if (IsDeleted) throw new InvalidOperationException("Deleted comments cannot be edited.");
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var normalized = content.Trim();
        if (normalized.Length > 4000) throw new ArgumentOutOfRangeException(nameof(content));
        Content = normalized;
        AuthoringLocale = authoringLocale;
        UpdatedAt = utcNow;
    }

    public void Delete(Guid requesterId)
    {
        if (requesterId != AuthorId) throw new InvalidOperationException("Only the comment author can delete it.");
        IsDeleted = true;
        Content = string.Empty;
    }
}