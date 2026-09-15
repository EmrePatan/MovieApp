using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Discovery;

public sealed class OnTvThisWeekValidatorTests
{
    [Fact]
    public void ValidateAcceptsDefaultPagination()
    {
        var result = OnTvThisWeekValidator.Validate(new OnTvThisWeekCriteria(1, 20));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidPageSize()
    {
        var result = OnTvThisWeekValidator.Validate(new OnTvThisWeekCriteria(1, 0));

        Assert.False(result.IsValid);
    }
}
