using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Library;

public sealed class PostgresGuidComparerTests
{
    [Fact]
    public void HighBitUuidsSortByUnsignedNetworkOrder()
    {
        var smaller = Guid.Parse("7fffffff-0000-0000-0000-000000000001");
        var larger = Guid.Parse("ffffffff-0000-0000-0000-000000000001");

        Assert.True(PostgresGuidComparer.Instance.Compare(smaller, larger) < 0);
        Assert.True(PostgresGuidComparer.Instance.Compare(larger, smaller) > 0);
        Assert.Equal(0, PostgresGuidComparer.Instance.Compare(smaller, smaller));
    }
}
