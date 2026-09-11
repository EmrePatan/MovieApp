using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class UserEntityTests
{
    [Fact]
    public void UpdateDisplayNameTrimsValue()
    {
        var user = CreateUser();

        user.UpdateDisplayName("  Updated Name  ", DateTime.UtcNow);

        Assert.Equal("Updated Name", user.DisplayName);
    }

    [Fact]
    public void ChangeEmailRotatesSecurityStamp()
    {
        var user = CreateUser();
        var originalStamp = user.SecurityStamp;

        user.ChangeEmail("new@example.com", "new@example.com", DateTime.UtcNow);

        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.Equal("new@example.com", user.Email);
    }

    [Fact]
    public void ChangePasswordRotatesSecurityStamp()
    {
        var user = CreateUser();
        var originalStamp = user.SecurityStamp;

        user.ChangePassword("new-hash", DateTime.UtcNow);

        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.Equal("new-hash", user.PasswordHash);
    }

    private static User CreateUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);
}
