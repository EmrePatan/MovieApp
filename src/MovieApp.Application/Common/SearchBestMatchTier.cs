using MovieApp.Domain.Enums;

namespace MovieApp.Application.Common;

/// <summary>
/// Lower values rank higher. Direct canonical exact/prefix/substring remain 0/1/2 for MC-002 compatibility.
/// </summary>
public static class SearchBestMatchTier
{
    public const int NoMatch = 99;

    public const int DirectExactCanonical = 0;
    public const int DirectPrefixCanonical = 1;
    public const int DirectSubstringCanonical = 2;

    public const int DirectExactOriginal = 3;
    public const int DirectPrefixOriginal = 4;
    public const int DirectSubstringOriginal = 5;

    public const int DirectExactAlias = 6;
    public const int DirectPrefixAlias = 7;
    public const int DirectSubstringAlias = 8;

    public const int FoldedExactCanonical = 9;
    public const int FoldedPrefixCanonical = 10;
    public const int FoldedSubstringCanonical = 11;

    public const int FoldedExactOriginal = 12;
    public const int FoldedPrefixOriginal = 13;
    public const int FoldedSubstringOriginal = 14;

    public const int FoldedExactAlias = 15;
    public const int FoldedPrefixAlias = 16;
    public const int FoldedSubstringAlias = 17;

    public static int KindBase(ContentSearchTitleKind kind) =>
        kind switch
        {
            ContentSearchTitleKind.Canonical => DirectExactCanonical,
            ContentSearchTitleKind.Original => DirectExactOriginal,
            _ => DirectExactAlias,
        };

    public static int ComputeDirectTier(string title, SearchQueryMatch match, int kindBase)
    {
        if (string.IsNullOrEmpty(title))
        {
            return NoMatch;
        }

        if (IsExact(title, match))
        {
            return kindBase;
        }

        if (IsPrefix(title, match))
        {
            return kindBase + 1;
        }

        if (IsSubstring(title, match))
        {
            return kindBase + 2;
        }

        return NoMatch;
    }

    public static int ComputeFoldedTier(string normalizedTitle, string foldedQuery, int kindBase)
    {
        if (string.IsNullOrEmpty(normalizedTitle) || string.IsNullOrEmpty(foldedQuery))
        {
            return NoMatch;
        }

        var foldedBase = kindBase + 9;

        if (normalizedTitle.Length == foldedQuery.Length
            && string.Equals(normalizedTitle, foldedQuery, StringComparison.Ordinal))
        {
            return foldedBase;
        }

        if (normalizedTitle.StartsWith(foldedQuery, StringComparison.Ordinal))
        {
            return foldedBase + 1;
        }

        if (normalizedTitle.Contains(foldedQuery, StringComparison.Ordinal))
        {
            return foldedBase + 2;
        }

        return NoMatch;
    }

    public static int Min(int left, int right) => left <= right ? left : right;

    public static int Min(int left, int middle, int right) => Min(Min(left, middle), right);

    private static bool IsExact(string title, SearchQueryMatch match) =>
        SearchTitleMatching.TitleEqualsQuery(title, match.Primary)
        || (match.TurkishAlternate is not null && SearchTitleMatching.TitleEqualsQuery(title, match.TurkishAlternate));

    private static bool IsPrefix(string title, SearchQueryMatch match) =>
        SearchTitleMatching.TitleStartsWithQuery(title, match.Primary)
        || (match.TurkishAlternate is not null && SearchTitleMatching.TitleStartsWithQuery(title, match.TurkishAlternate));

    private static bool IsSubstring(string title, SearchQueryMatch match) =>
        title.Contains(match.Primary, StringComparison.OrdinalIgnoreCase)
        || (match.TurkishAlternate is not null
            && title.Contains(match.TurkishAlternate, StringComparison.OrdinalIgnoreCase));
}
