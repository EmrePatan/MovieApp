using MovieApp.Application.Common;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Search;

public sealed class SearchBestMatchTierTests
{
    [Fact]
    public void DirectCanonicalExactOutranksAliasSubstring()
    {
        var match = SearchQueryMatch.FromQuery("whistle");
        var canonical = SearchBestMatchTier.ComputeDirectTier(
            "Whistle If You Come Back",
            match,
            SearchBestMatchTier.DirectExactCanonical);
        var alias = SearchBestMatchTier.ComputeDirectTier(
            "A Whistle Tale",
            match,
            SearchBestMatchTier.DirectExactAlias);

        Assert.Equal(SearchBestMatchTier.DirectPrefixCanonical, canonical);
        Assert.Equal(SearchBestMatchTier.DirectSubstringAlias, alias);
        Assert.True(canonical < alias);
    }

    [Fact]
    public void FoldedAliasTierIsWeakerThanDirectCanonicalExact()
    {
        var match = SearchQueryMatch.FromQuery("donersen isl");
        var directCanonical = SearchBestMatchTier.ComputeDirectTier(
            "donersen isl",
            match,
            SearchBestMatchTier.DirectExactCanonical);
        var foldedAlias = SearchBestMatchTier.ComputeFoldedTier(
            SearchTitleFolder.Fold("Dönersen Islık Çal"),
            match.Folded,
            SearchBestMatchTier.DirectExactAlias);

        Assert.Equal(SearchBestMatchTier.DirectExactCanonical, directCanonical);
        Assert.Equal(SearchBestMatchTier.FoldedPrefixAlias, foldedAlias);
        Assert.True(directCanonical < foldedAlias);
    }

    [Fact]
    public void KindBaseMapsTitleKindsToDirectBases()
    {
        Assert.Equal(SearchBestMatchTier.DirectExactCanonical, SearchBestMatchTier.KindBase(ContentSearchTitleKind.Canonical));
        Assert.Equal(SearchBestMatchTier.DirectExactOriginal, SearchBestMatchTier.KindBase(ContentSearchTitleKind.Original));
        Assert.Equal(SearchBestMatchTier.DirectExactAlias, SearchBestMatchTier.KindBase(ContentSearchTitleKind.Alternative));
        Assert.Equal(SearchBestMatchTier.DirectExactAlias, SearchBestMatchTier.KindBase(ContentSearchTitleKind.Translation));
    }
}
