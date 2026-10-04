namespace MovieApp.Application.Common;

/// <summary>
/// Display-time title comparison. Uses the same folding semantics as catalog search.
/// </summary>
public static class DisplayTitleEquivalence
{
    public static bool AreEquivalent(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var foldedLeft = NormalizeForEquivalence(left);
        var foldedRight = NormalizeForEquivalence(right);
        if (foldedLeft.Length == 0 || foldedRight.Length == 0)
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(foldedLeft, foldedRight, StringComparison.Ordinal);
    }

    private static string NormalizeForEquivalence(string title)
    {
        var folded = SearchTitleFolder.Fold(title);
        if (folded.Length == 0)
        {
            return folded;
        }

        return folded
            .Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("’", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("´", string.Empty, StringComparison.Ordinal);
    }
}
