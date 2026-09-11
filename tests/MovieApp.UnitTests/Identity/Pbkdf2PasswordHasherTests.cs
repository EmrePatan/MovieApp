using MovieApp.Infrastructure.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPasswordAndVerifyPasswordSucceedsForSamePassword()
    {
        const string password = "StrongPassword123";

        var hash = _hasher.HashPassword(password);

        Assert.True(_hasher.VerifyPassword(password, hash));
    }

    [Fact]
    public void VerifyPasswordFailsForWrongPassword()
    {
        var hash = _hasher.HashPassword("StrongPassword123");

        Assert.False(_hasher.VerifyPassword("WrongPassword123", hash));
    }

    [Fact]
    public void HashPasswordProducesUniqueHashesForSamePassword()
    {
        const string password = "StrongPassword123";

        var firstHash = _hasher.HashPassword(password);
        var secondHash = _hasher.HashPassword(password);

        Assert.NotEqual(firstHash, secondHash);
        Assert.True(_hasher.VerifyPassword(password, firstHash));
        Assert.True(_hasher.VerifyPassword(password, secondHash));
    }

    [Fact]
    public void HashPasswordThrowsForEmptyPassword()
    {
        Assert.Throws<ArgumentException>(() => _hasher.HashPassword(string.Empty));
    }

    [Fact]
    public void VerifyPasswordReturnsFalseForInvalidHashFormat()
    {
        Assert.False(_hasher.VerifyPassword("StrongPassword123", "invalid-hash"));
    }
}
