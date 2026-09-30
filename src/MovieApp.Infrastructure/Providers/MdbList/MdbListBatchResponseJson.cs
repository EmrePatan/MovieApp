using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.MdbList;

/// <summary>
/// Official MDBList Media Info Batch request body (POST /{media_provider}/{media_type}/?apikey=...).
/// </summary>
public sealed class MdbListBatchRequestJson
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = [];

    [JsonPropertyName("append_to_response")]
    public List<string>? AppendToResponse { get; set; }
}

public sealed class MdbListBatchTitleResponseJson
{
    [JsonPropertyName("ids")]
    public MdbListBatchMediaIdsJson? Ids { get; set; }

    [JsonPropertyName("keywords")]
    public List<MdbListKeywordJson>? Keywords { get; set; }

    [JsonPropertyName("ratings")]
    public List<MdbListRatingJson>? Ratings { get; set; }
}

public sealed class MdbListBatchMediaIdsJson
{
    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }
}
