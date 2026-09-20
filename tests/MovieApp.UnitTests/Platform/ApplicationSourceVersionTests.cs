using MovieApp.Application;

namespace MovieApp.UnitTests.Platform;

public sealed class ApplicationSourceVersionTests
{
    [Fact]
    public void ResolvePrefersEnvironmentVariableOverAssemblyMetadata()
    {
        var original = Environment.GetEnvironmentVariable(ApplicationSourceVersion.EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(ApplicationSourceVersion.EnvironmentVariableName, "ci-sha-123");

            Assert.Equal("ci-sha-123", ApplicationSourceVersion.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ApplicationSourceVersion.EnvironmentVariableName, original);
        }
    }

    [Fact]
    public void ResolveIgnoresUnknownEnvironmentPlaceholder()
    {
        var original = Environment.GetEnvironmentVariable(ApplicationSourceVersion.EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(ApplicationSourceVersion.EnvironmentVariableName, "unknown");

            var resolved = ApplicationSourceVersion.Resolve();

            Assert.False(string.IsNullOrWhiteSpace(resolved));
            Assert.NotEqual("unknown", resolved, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ApplicationSourceVersion.EnvironmentVariableName, original);
        }
    }
}
