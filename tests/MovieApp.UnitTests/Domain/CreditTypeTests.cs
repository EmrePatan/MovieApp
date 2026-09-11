using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Domain;

public sealed class CreditTypeTests
{
    [Fact]
    public void CreditTypeDistinguishesCastAndCrew()
    {
        Assert.NotEqual(CreditType.Cast, CreditType.Crew);
        Assert.Equal(1, (int)CreditType.Cast);
        Assert.Equal(2, (int)CreditType.Crew);
    }
}
