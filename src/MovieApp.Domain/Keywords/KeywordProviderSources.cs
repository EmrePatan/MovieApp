using System.Text.Json;
using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Keywords;

public static class KeywordProviderSources
{
    public const string Empty = "[]";

    public static string Create(KeywordProvider provider) =>
        JsonSerializer.Serialize(new[] { ToStorageName(provider) });

    public static bool Contains(string? sources, KeywordProvider provider) =>
        Read(sources).Contains(ToStorageName(provider), StringComparer.OrdinalIgnoreCase);

    public static bool HasAny(string? sources) => Read(sources).Count > 0;

    public static string SetProvider(string? sources, KeywordProvider provider, bool include)
    {
        var values = Read(sources);
        var storageName = ToStorageName(provider);
        var contains = values.Contains(storageName, StringComparer.OrdinalIgnoreCase);

        if (contains == include)
        {
            return sources ?? Empty;
        }

        if (include)
        {
            values.Add(storageName);
        }
        else
        {
            values.RemoveAll(value => string.Equals(value, storageName, StringComparison.OrdinalIgnoreCase));
        }

        values.Sort(StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(values);
    }

    public static string ToStorageName(KeywordProvider provider) =>
        provider.ToString().ToLowerInvariant();

    private static List<string> Read(string? sources)
    {
        if (string.IsNullOrWhiteSpace(sources))
        {
            return [];
        }

        var values = JsonSerializer.Deserialize<List<string>>(sources)
            ?? throw new JsonException("Keyword provider sources must be a JSON array.");

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
