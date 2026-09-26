using System.Text;

namespace MovieApp.Application.Common;

public static class QueryNormalizer
{
    public static string Normalize(string query)
    {
        return CollapseWhitespace(query.Trim()).Normalize(NormalizationForm.FormC);
    }

    public static string CollapseWhitespace(string query)
    {
        return string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
