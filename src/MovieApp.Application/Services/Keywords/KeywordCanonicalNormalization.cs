using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MovieApp.Application.Services.Keywords;

public static class KeywordCanonicalNormalization
{
    private static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string NormalizeKeywordName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var trimmed = name.Trim();
        var normalizedForm = trimmed.Normalize(NormalizationForm.FormKC);
        var lower = normalizedForm.ToLowerInvariant();
        var separators = lower.Replace('-', ' ').Replace('_', ' ');
        var collapsed = WhitespaceCollapse.Replace(separators, " ").Trim();
        return collapsed;
    }
}
