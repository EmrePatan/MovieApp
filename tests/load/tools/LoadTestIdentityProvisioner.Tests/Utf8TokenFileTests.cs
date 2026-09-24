using System.Text;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class Utf8TokenFileTests
{
    [Fact]
    public void Utf8WithoutBomStartsWithObjectBrace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tokens-{Guid.NewGuid():N}.json");
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(path, "{\"identities\":[]}", utf8NoBom);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(0x7B, bytes[0]);
        File.Delete(path);
    }
}
