using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class DirectMessageTests
{
    [Fact]
    public void CreateStoresTrimmedBody()
    {
        var conversationId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var message = DirectMessage.Create(conversationId, senderId, "  hello  ", now);

        Assert.Equal(conversationId, message.ConversationId);
        Assert.Equal(senderId, message.SenderId);
        Assert.Equal("hello", message.Body);
        Assert.Equal(now, message.CreatedAt);
    }

    [Fact]
    public void CreateRejectsEmptyAndOversizedBodies()
    {
        var conversationId = Guid.NewGuid();
        var senderId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            DirectMessage.Create(conversationId, senderId, "   ", DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DirectMessage.Create(conversationId, senderId, new string('a', DirectMessage.MaxBodyLength + 1), DateTime.UtcNow));
    }

    [Fact]
    public void ParticipantReadCursorOnlyMovesForward()
    {
        var participant = DirectConversationParticipant.Create(Guid.NewGuid(), Guid.NewGuid());
        var first = DateTime.UtcNow;
        var earlier = first.AddMinutes(-5);

        participant.MarkRead(first);
        participant.MarkRead(earlier);

        Assert.Equal(first, participant.LastReadAt);
    }
}
