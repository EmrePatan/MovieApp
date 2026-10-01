using System.Text.RegularExpressions;
using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.Keywords;

public static partial class KeywordDisplayQualityEvaluator
{
    private static readonly string[] RelationshipBoilerplateFragments =
    [
        "relationship",
        "father son",
        "father daughter",
        "mother son",
        "mother daughter",
        "husband wife",
        "wife husband",
        "boyfriend girlfriend",
        "girlfriend boyfriend",
    ];

    private static readonly string[] IncidentalObjectOrEventFragments =
    [
        "elevator",
        "photograph",
        "car accident",
        "car theft",
        "acid",
        "corpse",
        "shot to death",
        "kiss",
        "chase",
        "explosion",
        "gun",
        "blood",
        "murder",
        "violence",
        "death",
        "fight",
        "escape",
        "deception",
        "fear",
        "dog",
        "photograph",
    ];

    private static readonly string[] ThematicBoostFragments =
    [
        "zombie",
        "apocalypse",
        "survival",
        "time travel",
        "revenge",
        "horror",
        "virus",
        "mutation",
        "alien",
        "dystopia",
        "heist",
        "conspiracy",
    ];

    public static KeywordDisplayQualityResult Evaluate(
        string canonicalName,
        int documentFrequency,
        int catalogDocumentCount,
        int movieTitleCount,
        int tvTitleCount,
        KeywordDisplayProfileOptions options)
    {
        var normalized = Normalize(canonicalName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return KeywordDisplayQualityResult.NotDisplayable;
        }

        if (normalized.Length < options.MinimumNameLength ||
            normalized.Length > options.MaximumNameLength)
        {
            return KeywordDisplayQualityResult.NotDisplayable;
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || tokens.Length > options.MaximumTokenCount)
        {
            return KeywordDisplayQualityResult.NotDisplayable;
        }

        if (documentFrequency < options.MinimumDocumentFrequency)
        {
            return KeywordDisplayQualityResult.NotDisplayable;
        }

        var dfRatio = documentFrequency / (double)Math.Max(1, catalogDocumentCount);
        if (dfRatio >= options.MaximumGenericDocumentFrequencyRatio)
        {
            return KeywordDisplayQualityResult.NotDisplayable;
        }

        if (ContainsAny(normalized, RelationshipBoilerplateFragments) ||
            ContainsAny(normalized, IncidentalObjectOrEventFragments) ||
            LooksLikePersonName(normalized, tokens) ||
            NumericOnly(tokens))
        {
            return KeywordDisplayQualityResult.NotDisplayable;
        }

        var coverage = movieTitleCount + tvTitleCount;
        var rank = ComputeDisplayRank(
            normalized,
            tokens.Length,
            documentFrequency,
            dfRatio,
            coverage,
            movieTitleCount,
            tvTitleCount);

        return new KeywordDisplayQualityResult(true, rank);
    }

    private static int ComputeDisplayRank(
        string normalized,
        int tokenCount,
        int documentFrequency,
        double documentFrequencyRatio,
        int coverage,
        int movieTitleCount,
        int tvTitleCount)
    {
        var rank = 100;

        if (ContainsAny(normalized, ThematicBoostFragments))
        {
            rank += 80;
        }

        if (tokenCount is 2 or 3)
        {
            rank += 25;
        }
        else if (tokenCount == 1)
        {
            rank += 10;
        }

        rank += (int)Math.Round(Math.Log(documentFrequency + 1) * 12);

        if (movieTitleCount > 0 && tvTitleCount > 0)
        {
            rank += 20;
        }

        rank += Math.Min(30, coverage);

        rank -= (int)Math.Round(documentFrequencyRatio * 200);

        return Math.Max(1, rank);
    }

    private static bool ContainsAny(string normalized, IEnumerable<string> fragments)
    {
        foreach (var fragment in fragments)
        {
            if (normalized.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikePersonName(string normalized, string[] tokens)
    {
        if (tokens.Length != 2)
        {
            return false;
        }

        if (ContainsAny(normalized, ThematicBoostFragments))
        {
            return false;
        }

        return PersonNameToken().IsMatch(tokens[0]) && PersonNameToken().IsMatch(tokens[1]);
    }

    private static bool NumericOnly(string[] tokens) =>
        tokens.Length == 1 && tokens[0].All(char.IsDigit);

    /// <summary>
    /// Extra detail-page themes when too few primary displayable keywords exist on a title.
    /// Keeps blocklists and shape rules; does not require catalog document-frequency thresholds.
    /// </summary>
    public static bool IsEligibleForDetailSupplemental(
        string canonicalName,
        KeywordDisplayProfileOptions options)
    {
        var normalized = Normalize(canonicalName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.Length < options.MinimumNameLength ||
            normalized.Length > options.MaximumNameLength)
        {
            return false;
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || tokens.Length > options.MaximumTokenCount)
        {
            return false;
        }

        if (ContainsAny(normalized, RelationshipBoilerplateFragments) ||
            ContainsAny(normalized, IncidentalObjectOrEventFragments) ||
            LooksLikePersonName(normalized, tokens) ||
            NumericOnly(tokens))
        {
            return false;
        }

        return true;
    }

    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    [GeneratedRegex("^[a-z][a-z'-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PersonNameToken();
}

public sealed record KeywordDisplayQualityResult(bool Displayable, int DisplayRank)
{
    public static KeywordDisplayQualityResult NotDisplayable => new(false, 0);
}
