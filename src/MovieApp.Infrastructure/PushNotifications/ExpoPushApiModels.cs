using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.PushNotifications;

internal sealed class ExpoPushSendRequestItem
{
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public Dictionary<string, string> Data { get; set; } = [];
}

internal sealed class ExpoPushSendResponse
{
    [JsonPropertyName("data")]
    public List<ExpoPushTicketResponseItem> Data { get; set; } = [];
}

internal sealed class ExpoPushTicketResponseItem
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("details")]
    public ExpoPushErrorDetails? Details { get; set; }
}

internal sealed class ExpoPushReceiptRequest
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = [];
}

internal sealed class ExpoPushReceiptResponse
{
    [JsonPropertyName("data")]
    public Dictionary<string, ExpoPushReceiptResponseItem> Data { get; set; } = [];
}

internal sealed class ExpoPushReceiptResponseItem
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("details")]
    public ExpoPushErrorDetails? Details { get; set; }
}

internal sealed class ExpoPushErrorDetails
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
