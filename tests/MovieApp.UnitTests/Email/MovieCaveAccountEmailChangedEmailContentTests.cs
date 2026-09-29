using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class MovieCaveAccountEmailChangedEmailContentTests
{
    [Fact]
    public void GetSubject_ReturnsLocalizedCopyForTurkish()
    {
        Assert.Contains(
            "Movie Cave",
            MovieCaveAccountEmailChangedEmailContent.GetSubject("tr-TR"));
    }

    [Fact]
    public void BuildPlainText_DoesNotIncludeSecrets()
    {
        var body = MovieCaveAccountEmailChangedEmailContent.BuildPlainText("en-US");

        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", body, StringComparison.OrdinalIgnoreCase);
    }
}
