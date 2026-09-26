namespace MovieApp.Application.Common;

public readonly record struct SearchQueryMatch(SearchTextMatch Text, string Folded)
{
    public static readonly SearchQueryMatch Empty = new(SearchTextMatch.Empty, string.Empty);

    public bool IsEmpty => Text.IsEmpty;

    public string Primary => Text.Primary;

    public string? TurkishAlternate => Text.TurkishAlternate;

    public static SearchQueryMatch FromQuery(string? query)
    {
        var text = SearchTextMatch.FromQuery(query);
        if (text.IsEmpty)
        {
            return Empty;
        }

        return new SearchQueryMatch(text, SearchTitleFolder.Fold(text.Primary));
    }

    public static SearchQueryMatch From(SearchTextMatch text)
    {
        if (text.IsEmpty)
        {
            return Empty;
        }

        return new SearchQueryMatch(text, SearchTitleFolder.Fold(text.Primary));
    }
}
