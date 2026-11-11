using System.Globalization;
using System.Text;

namespace MovieApp.Application.Common;

/// <summary>
/// Search-only title folding. Never use for display titles.
/// </summary>
public static class SearchTitleFolder
{
    public static string Fold(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var collapsed = QueryNormalizer.CollapseWhitespace(title.Trim());
        if (collapsed.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(collapsed.Length * 2);

        // Enumerating Runes keeps valid surrogate pairs intact and maps malformed UTF-16
        // to Rune.ReplacementChar instead of letting Normalize throw on provider data.
        foreach (var character in collapsed.EnumerateRunes())
        {
            if (TryMapTurkishSearchCharacter(character, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            if (character.Value is 'ß' or 'ẞ')
            {
                builder.Append("ss");
                continue;
            }

            var decomposed = character.ToString().Normalize(NormalizationForm.FormD);
            var hasBase = false;
            foreach (var decomposedCharacter in decomposed.EnumerateRunes())
            {
                if (Rune.GetUnicodeCategory(decomposedCharacter) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (TryMapTurkishSearchCharacter(decomposedCharacter, out var mappedDecomposed))
                {
                    builder.Append(mappedDecomposed);
                }
                else
                {
                    builder.Append(Rune.ToLowerInvariant(decomposedCharacter).ToString());
                }

                hasBase = true;
            }

            if (!hasBase)
            {
                builder.Append(Rune.ToLowerInvariant(character).ToString());
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool TryMapTurkishSearchCharacter(Rune character, out string mapped)
    {
        switch (character.Value)
        {
            case 'ı':
                mapped = "i";
                return true;
            case 'İ':
                mapped = "i";
                return true;
            case 'ş':
            case 'Ş':
                mapped = "s";
                return true;
            case 'ğ':
            case 'Ğ':
                mapped = "g";
                return true;
            case 'ü':
            case 'Ü':
                mapped = "u";
                return true;
            case 'ö':
            case 'Ö':
                mapped = "o";
                return true;
            case 'ç':
            case 'Ç':
                mapped = "c";
                return true;
            default:
                mapped = string.Empty;
                return false;
        }
    }
}
