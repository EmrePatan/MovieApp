using MovieApp.Application.Messages;

namespace MovieApp.UnitTests.Messages;

public sealed class DirectMessagePageWindowTests
{
    [Fact]
    public void FirstPageReturnsTheNewestWindowOldestFirst()
    {
        var window = DirectMessagePageWindow.Resolve(totalCount: 25, page: 1, pageSize: 20);

        Assert.Equal(5, window.Skip);
        Assert.Equal(20, window.Take);
        Assert.True(window.HasMore);
    }

    [Fact]
    public void SecondPageReturnsTheOlderRemainder()
    {
        var window = DirectMessagePageWindow.Resolve(totalCount: 25, page: 2, pageSize: 20);

        Assert.Equal(0, window.Skip);
        Assert.Equal(5, window.Take);
        Assert.False(window.HasMore);
    }

    [Fact]
    public void EmptyAndOutOfRangePagesTakeNothing()
    {
        Assert.Equal((0, 0, false), DirectMessagePageWindow.Resolve(0, 1, 20));
        Assert.Equal((0, 0, false), DirectMessagePageWindow.Resolve(10, 3, 20));
    }
}
