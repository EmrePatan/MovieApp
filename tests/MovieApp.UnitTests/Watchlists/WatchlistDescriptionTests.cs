using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Watchlists;

public sealed class WatchlistDescriptionTests
{
    private static readonly DateTime UtcNow = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Sunday movies  ", "Sunday movies")]
    public void NormalizeDescriptionTrimsAndMapsBlankToNull(string? input, string? expected)
    {
        Assert.Equal(expected, Watchlist.NormalizeDescription(input));
    }

    [Fact]
    public void NormalizeDescriptionAcceptsExactlyTheMaximumLength()
    {
        var description = new string('a', Watchlist.MaxDescriptionLength);

        Assert.Equal(description, Watchlist.NormalizeDescription(description));
    }

    [Fact]
    public void NormalizeDescriptionRejectsTooLongText()
    {
        var description = new string('a', Watchlist.MaxDescriptionLength + 1);

        Assert.Throws<ArgumentException>(() => Watchlist.NormalizeDescription(description));
    }

    [Fact]
    public void CreateStoresTrimmedDescription()
    {
        var watchlist = Watchlist.Create(Guid.NewGuid(), "Weekend", UtcNow, " Cozy picks ");

        Assert.Equal("Cozy picks", watchlist.Description);
    }

    [Fact]
    public void CreateWithoutDescriptionLeavesItEmpty()
    {
        var watchlist = Watchlist.Create(Guid.NewGuid(), "Weekend", UtcNow);

        Assert.Null(watchlist.Description);
    }

    [Fact]
    public void SetDescriptionWithBlankClearsExistingText()
    {
        var watchlist = Watchlist.Create(Guid.NewGuid(), "Weekend", UtcNow, "Cozy picks");

        watchlist.SetDescription("   ", UtcNow.AddMinutes(1));

        Assert.Null(watchlist.Description);
        Assert.Equal(UtcNow.AddMinutes(1), watchlist.UpdatedAt);
    }

    [Fact]
    public void ValidatorRejectsDescriptionOverTheLimitAndAllowsNull()
    {
        Assert.True(WatchlistDescriptionValidator.Validate(null).IsValid);
        Assert.True(WatchlistDescriptionValidator.Validate(new string('a', Watchlist.MaxDescriptionLength)).IsValid);
        Assert.False(WatchlistDescriptionValidator.Validate(new string('a', Watchlist.MaxDescriptionLength + 1)).IsValid);
    }
}
