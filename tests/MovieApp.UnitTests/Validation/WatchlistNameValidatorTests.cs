using MovieApp.Application.Validation;
using MovieApp.Domain.Watchlists;

namespace MovieApp.UnitTests.Validation;

public sealed class WatchlistNameValidatorTests
{
    [Fact]
    public void ValidateReturnsSuccessForValidName()
    {
        var result = WatchlistNameValidator.Validate("Weekend Watch");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFailsForEmptyName()
    {
        var result = WatchlistNameValidator.Validate("   ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateFailsWhenNameExceedsMaxLength()
    {
        var result = WatchlistNameValidator.Validate(new string('a', WatchlistNameNormalizer.MaxLength + 1));

        Assert.False(result.IsValid);
    }
}
