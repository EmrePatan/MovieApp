using System.Text.Json;

namespace MovieApp.Infrastructure.Email;

internal static class ResendApiErrorReader
{
    internal static async Task<string?> TryReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (response.Content is null)
        {
            return null;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }

            if (root.TryGetProperty("error", out var errorElement)
                && errorElement.ValueKind == JsonValueKind.String)
            {
                return errorElement.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
