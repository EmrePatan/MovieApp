using MovieApp.Domain;

namespace MovieApp.UnitTests.Domain;

public sealed class AssemblyReferenceTests
{
    [Fact]
    public void DomainAssemblyReferenceIsAccessible()
    {
        var assemblyReference = typeof(AssemblyReference);

        Assert.NotNull(assemblyReference);
        Assert.Equal("MovieApp.Domain", assemblyReference.Assembly.GetName().Name);
    }
}
