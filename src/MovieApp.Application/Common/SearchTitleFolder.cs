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

        var normalized = collapsed.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length * 2);

        foreach (var character in normalized)
        {
            if (TryMapTurkishSearchCharacter(character, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            if (character == 'ß')
            {
                builder.Append("ss");
                continue;
            }

            if (character == 'ẞ')
            {
                builder.Append("ss");
                continue;
            }

            var decomposed = character.ToString().Normalize(NormalizationForm.FormD);
            var hasBase = false;
            foreach (var decomposedCharacter in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(decomposedCharacter) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (TryMapTurkishSearchCharacter(decomposedCharacter, out var mappedDecomposed))
                {
                    builder.Append(mappedDecomposed);
                }
                else
                {
                    builder.Append(char.ToLowerInvariant(decomposedCharacter));
                }

                hasBase = true;
            }

            if (!hasBase)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool TryMapTurkishSearchCharacter(char character, out string mapped)
    {
        switch (character)
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
