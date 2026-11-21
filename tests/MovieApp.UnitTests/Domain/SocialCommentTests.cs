using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class SocialCommentTests
{
    [Fact]
    public void CreateSupportsNestedReplies()
    {
        var activityId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        var comment = SocialComment.Create(activityId, authorId, parentId, "  Reply  ", "en-US", DateTime.UtcNow);

        Assert.Equal(activityId, comment.ActivityId);
        Assert.Equal(authorId, comment.AuthorId);
        Assert.Equal(parentId, comment.ParentCommentId);
        Assert.Equal("Reply", comment.Content);
        Assert.False(comment.IsDeleted);
    }

    [Fact]
    public void CreateRejectsEmptyAndOversizedContent()
    {
        Assert.Throws<ArgumentException>(() =>
            SocialComment.Create(Guid.NewGuid(), Guid.NewGuid(), null, "  ", "en-US", DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SocialComment.Create(Guid.NewGuid(), Guid.NewGuid(), null, new string('x', 4001), "en-US", DateTime.UtcNow));
    }

    [Fact]
    public void OnlyAuthorCanEditAndDelete()
    {
        var authorId = Guid.NewGuid();
        var comment = SocialComment.Create(Guid.NewGuid(), authorId, null, "Original", "tr-TR", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => comment.Edit(Guid.NewGuid(), "Changed", "tr-TR", DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => comment.Delete(Guid.NewGuid()));

        comment.Edit(authorId, "Changed", "en-US", DateTime.UtcNow);
        Assert.Equal("Changed", comment.Content);
        Assert.Equal("en-US", comment.AuthoringLocale);
        Assert.NotNull(comment.UpdatedAt);

        comment.Delete(authorId);
        Assert.True(comment.IsDeleted);
        Assert.Equal(string.Empty, comment.Content);
        Assert.Throws<InvalidOperationException>(() => comment.Edit(authorId, "Again", "en-US", DateTime.UtcNow));
    }
}