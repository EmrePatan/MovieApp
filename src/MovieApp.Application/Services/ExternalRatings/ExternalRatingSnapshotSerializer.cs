using System.Text.Json;
using MovieApp.Application.Models.ExternalRatings;

namespace MovieApp.Application.Services.ExternalRatings;

public static class ExternalRatingSnapshotSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(ExternalRatingSnapshotPayload payload) =>
        JsonSerializer.Serialize(payload, SerializerOptions);

    public static ExternalRatingSnapshotPayload Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ExternalRatingSnapshotPayload();
        }

        return JsonSerializer.Deserialize<ExternalRatingSnapshotPayload>(json, SerializerOptions)
            ?? new ExternalRatingSnapshotPayload();
    }
}
