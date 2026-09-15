using MovieApp.Application.Models.RegionalRelease;
using MovieApp.Application.Services.RegionalRelease;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.RegionalRelease;

public sealed class RegionalEffectiveReleaseResolverTests
{
    private readonly RegionalEffectiveReleaseResolver _resolver = new();

    [Fact]
    public void Resolve_TheatricalOnly_UsesTheatricalDate()
    {
        var result = _resolver.Resolve(
            "TR",
            [Entry("TR", new DateOnly(2026, 10, 10), TmdbReleaseType.Theatrical, "13+")],
            new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 10, 10), result.EffectiveReleaseDate);
        Assert.Equal(TmdbReleaseType.Theatrical, result.EffectiveReleaseType);
        Assert.Equal("13+", result.Certification);
        Assert.False(result.IsFallbackGlobal);
    }

    [Fact]
    public void Resolve_DigitalOnly_UsesDigitalDate()
    {
        var result = _resolver.Resolve(
            "TR",
            [Entry("TR", new DateOnly(2026, 11, 1), TmdbReleaseType.Digital, "16+")],
            new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 11, 1), result.EffectiveReleaseDate);
        Assert.Equal(TmdbReleaseType.Digital, result.EffectiveReleaseType);
        Assert.False(result.IsFallbackGlobal);
    }

    [Fact]
    public void Resolve_TheatricalEarlierThanDigital_UsesTheatrical()
    {
        var result = _resolver.Resolve(
            "TR",
            [
                Entry("TR", new DateOnly(2026, 10, 10), TmdbReleaseType.Theatrical, null),
                Entry("TR", new DateOnly(2026, 10, 20), TmdbReleaseType.Digital, null)
            ],
            null);

        Assert.Equal(new DateOnly(2026, 10, 10), result.EffectiveReleaseDate);
        Assert.Equal(TmdbReleaseType.Theatrical, result.EffectiveReleaseType);
    }

    [Fact]
    public void Resolve_DigitalEarlierThanTheatrical_UsesDigital()
    {
        var result = _resolver.Resolve(
            "TR",
            [
                Entry("TR", new DateOnly(2026, 10, 1), TmdbReleaseType.Digital, null),
                Entry("TR", new DateOnly(2026, 12, 1), TmdbReleaseType.Theatrical, null)
            ],
            null);

        Assert.Equal(new DateOnly(2026, 10, 1), result.EffectiveReleaseDate);
        Assert.Equal(TmdbReleaseType.Digital, result.EffectiveReleaseType);
    }

    [Fact]
    public void Resolve_SameDateTheatricalAndDigital_PrefersTheatrical()
    {
        var result = _resolver.Resolve(
            "TR",
            [
                Entry("TR", new DateOnly(2026, 10, 10), TmdbReleaseType.Digital, "16+", 1),
                Entry("TR", new DateOnly(2026, 10, 10), TmdbReleaseType.Theatrical, "13+", 0)
            ],
            null);

        Assert.Equal(TmdbReleaseType.Theatrical, result.EffectiveReleaseType);
        Assert.Equal("13+", result.Certification);
    }

    [Fact]
    public void Resolve_PremiereIgnored_FallsBackToGlobal()
    {
        var result = _resolver.Resolve(
            "TR",
            [Entry("TR", new DateOnly(2026, 9, 1), TmdbReleaseType.Premiere, null)],
            new DateOnly(2026, 10, 1));

        Assert.Equal(new DateOnly(2026, 10, 1), result.EffectiveReleaseDate);
        Assert.Null(result.EffectiveReleaseType);
        Assert.True(result.IsFallbackGlobal);
    }

    [Fact]
    public void Resolve_LimitedTheatricalIgnored_FallsBackToGlobal()
    {
        var result = _resolver.Resolve(
            "TR",
            [Entry("TR", new DateOnly(2026, 9, 1), TmdbReleaseType.LimitedTheatrical, null)],
            new DateOnly(2026, 10, 1));

        Assert.True(result.IsFallbackGlobal);
    }

    [Fact]
    public void Resolve_PhysicalAndTvIgnored_FallsBackToGlobal()
    {
        var result = _resolver.Resolve(
            "TR",
            [
                Entry("TR", new DateOnly(2026, 9, 1), TmdbReleaseType.Physical, null),
                Entry("TR", new DateOnly(2026, 9, 2), TmdbReleaseType.Tv, null)
            ],
            new DateOnly(2026, 10, 1));

        Assert.True(result.IsFallbackGlobal);
    }

    [Fact]
    public void Resolve_OtherRegionIgnored()
    {
        var result = _resolver.Resolve(
            "TR",
            [Entry("US", new DateOnly(2026, 10, 1), TmdbReleaseType.Theatrical, null)],
            new DateOnly(2026, 11, 1));

        Assert.Equal(new DateOnly(2026, 11, 1), result.EffectiveReleaseDate);
        Assert.True(result.IsFallbackGlobal);
    }

    [Fact]
    public void Resolve_NoTrConsumerRelease_UsesGlobalFallback()
    {
        var result = _resolver.Resolve("TR", [], new DateOnly(2026, 10, 1));

        Assert.Equal(new DateOnly(2026, 10, 1), result.EffectiveReleaseDate);
        Assert.Null(result.EffectiveReleaseType);
        Assert.True(result.IsFallbackGlobal);
    }

    [Fact]
    public void Resolve_NoTrAndNoGlobal_ReturnsSuccessfulNullEffectiveDate()
    {
        var result = _resolver.Resolve("TR", [], null);

        Assert.Null(result.EffectiveReleaseDate);
        Assert.True(result.IsFallbackGlobal);
    }

    [Fact]
    public void Resolve_BlankCertification_NormalizedToNull()
    {
        var result = _resolver.Resolve(
            "TR",
            [Entry("TR", new DateOnly(2026, 10, 1), TmdbReleaseType.Digital, "   ")],
            null);

        Assert.Null(result.Certification);
    }

    [Fact]
    public void Resolve_SameDateAndType_PrefersNonEmptyCertification()
    {
        var result = _resolver.Resolve(
            "TR",
            [
                Entry("TR", new DateOnly(2026, 10, 1), TmdbReleaseType.Digital, null, 0),
                Entry("TR", new DateOnly(2026, 10, 1), TmdbReleaseType.Digital, "16+", 1)
            ],
            null);

        Assert.Equal("16+", result.Certification);
    }

    [Fact]
    public void Resolve_DeterministicAcrossRepeatedCalls()
    {
        RegionalMovieReleaseEntry[] entries =
        [
            Entry("TR", new DateOnly(2026, 10, 1), TmdbReleaseType.Digital, null, 0),
            Entry("TR", new DateOnly(2026, 10, 1), TmdbReleaseType.Digital, null, 1)
        ];

        var first = _resolver.Resolve("TR", entries, null);
        var second = _resolver.Resolve("TR", entries, null);

        Assert.Equal(first, second);
    }

    private static RegionalMovieReleaseEntry Entry(
        string region,
        DateOnly releaseDate,
        TmdbReleaseType type,
        string? certification,
        int sourceIndex = 0) =>
        new(region, releaseDate, type, certification, sourceIndex);
}
