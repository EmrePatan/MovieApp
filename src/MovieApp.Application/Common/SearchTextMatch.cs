namespace MovieApp.Application.Common;

public readonly record struct SearchTextMatch(string Primary, string? TurkishAlternate)
{
    public static readonly SearchTextMatch Empty = new(string.Empty, null);

    public bool IsEmpty => string.IsNullOrEmpty(Primary);

    public static SearchTextMatch FromQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Empty;
        }

        var primary = QueryNormalizer.Normalize(query);
        var turkishAlternate = SearchTurkishCaseFolder.TryCreateAlternate(primary);
        return new SearchTextMatch(primary, turkishAlternate);
    }
}
