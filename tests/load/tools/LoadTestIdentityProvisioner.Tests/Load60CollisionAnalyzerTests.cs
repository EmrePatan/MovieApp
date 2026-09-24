using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;
using MovieApp.LoadTestIdentityProvisioner;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class Load60CollisionAnalyzerTests
{
    [Fact]
    public void AnalyzeBlocksNonLoad60Collision()
    {
        var slots = Load60IdentityFormats.BuildSlots(2, Load60IdentityFormats.DefaultEmailDomain);
        var existing = new Dictionary<string, User>
        {
            [slots[0].NormalizedEmail] = new User
            {
                Id = Guid.NewGuid(),
                Email = "customer@example.com",
                NormalizedEmail = slots[0].NormalizedEmail,
                UserName = "customer",
                DisplayName = "Real User",
                PasswordHash = "x",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid(),
                EmailVerifiedAtUtc = DateTime.UtcNow,
            },
        };

        var analysis = Load60CollisionAnalyzer.Analyze(slots, existing, Load60IdentityFormats.DefaultEmailDomain);
        Assert.False(analysis.CanWrite);
        Assert.Contains(analysis.Slots, s => s.Status == Load60SlotStatus.BlockedCollision);
    }

    [Fact]
    public void AnalyzeTreatsExistingDedicatedAsIdempotent()
    {
        var slots = Load60IdentityFormats.BuildSlots(1, Load60IdentityFormats.DefaultEmailDomain);
        var slot = slots[0];
        var user = User.Create(Guid.NewGuid(), slot.Email, "100000.x.y", slot.DisplayName, DateTime.UtcNow);
        user.MarkEmailVerified(DateTime.UtcNow);
        var existing = new Dictionary<string, User> { [slot.NormalizedEmail] = user };

        var analysis = Load60CollisionAnalyzer.Analyze(slots, existing, Load60IdentityFormats.DefaultEmailDomain);
        Assert.True(analysis.CanWrite);
        Assert.Equal(Load60SlotStatus.ExistingDedicated, analysis.Slots[0].Status);
    }
}
