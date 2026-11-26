using System.Text.Json;
using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Mapping;

/// <summary>Storage form of <see cref="CatalogDetailExtras"/>. A null column means "never fetched".</summary>
public static class CatalogDetailExtrasJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(CatalogDetailExtras extras) => JsonSerializer.Serialize(extras, Options);

    public static CatalogDetailExtras? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CatalogDetailExtras>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
