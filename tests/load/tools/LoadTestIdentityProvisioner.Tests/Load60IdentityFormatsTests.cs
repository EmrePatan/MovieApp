using MovieApp.LoadTestIdentityProvisioner;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class Load60IdentityFormatsTests
{
    [Fact]
    public void DefaultReservedDomainIsAcceptedByEmailValidation()
    {
        Assert.True(Load60IdentityFormats.IsReservedDomainSupported(Load60IdentityFormats.DefaultEmailDomain));
    }

    [Fact]
    public void BuildSlotsProducesDeterministicFiftyIdentities()
    {
        var slots = Load60IdentityFormats.BuildSlots(50, Load60IdentityFormats.DefaultEmailDomain);
        Assert.Equal(50, slots.Count);
        Assert.Equal("load60-001", slots[0].HarnessId);
        Assert.Equal("load60-001@loadtest.invalid", slots[0].Email);
        Assert.Equal("LOAD60 #001", slots[0].DisplayName);
        Assert.Equal("load60-050", slots[49].HarnessId);
    }

    [Fact]
    public void DedicatedLoad60DetectorRejectsUnrelatedEmail()
    {
        Assert.False(Load60IdentityFormats.IsDedicatedLoad60NormalizedEmail(
            "real.user@example.com",
            Load60IdentityFormats.DefaultEmailDomain));
    }
}
