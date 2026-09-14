using System.Net.Http.Json;
using System.Text.Json;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Application.Services.PushNotifications;

namespace MovieApp.Infrastructure.PushNotifications;

public sealed class ExpoPushClient(HttpClient httpClient) : IExpoPushClient
{
    private const int MaxBatchSize = 100;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<ExpoPushSendResult>> SendAsync(
        IReadOnlyList<PushNotificationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var results = new List<ExpoPushSendResult>(messages.Count);

        foreach (var batch in messages.Chunk(MaxBatchSize))
        {
            var requestItems = batch
                .Select(message => new ExpoPushSendRequestItem
                {
                    To = message.ExpoPushToken,
                    Title = message.Title,
                    Body = message.Body,
                    Data = message.Data.ToDictionary(pair => pair.Key, pair => pair.Value)
                })
                .ToList();

            using var response = await httpClient.PostAsJsonAsync(
                "push/send",
                requestItems,
                SerializerOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ExpoPushSendResponse>(
                SerializerOptions,
                cancellationToken);

            if (payload?.Data is null || payload.Data.Count != batch.Length)
            {
                throw new InvalidOperationException("Expo push send response was malformed or incomplete.");
            }

            for (var index = 0; index < batch.Length; index++)
            {
                var message = batch[index];
                var ticket = payload.Data[index];
                results.Add(MapTicketResult(message.DeliveryId, ticket));
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<ExpoPushReceiptResult>> GetReceiptsAsync(
        IReadOnlyCollection<string> ticketIds,
        CancellationToken cancellationToken = default)
    {
        if (ticketIds.Count == 0)
        {
            return [];
        }

        var results = new List<ExpoPushReceiptResult>();

        foreach (var batch in ticketIds.Distinct().Chunk(MaxBatchSize))
        {
            using var response = await httpClient.PostAsJsonAsync(
                "push/getReceipts",
                new ExpoPushReceiptRequest { Ids = batch.ToList() },
                SerializerOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ExpoPushReceiptResponse>(
                SerializerOptions,
                cancellationToken);

            if (payload?.Data is null)
            {
                throw new InvalidOperationException("Expo push receipt response was malformed.");
            }

            foreach (var ticketId in batch)
            {
                if (!payload.Data.TryGetValue(ticketId, out var receipt))
                {
                    continue;
                }

                results.Add(MapReceiptResult(ticketId, receipt));
            }
        }

        return results;
    }

    private static ExpoPushSendResult MapTicketResult(Guid deliveryId, ExpoPushTicketResponseItem ticket)
    {
        if (string.Equals(ticket.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return new ExpoPushSendResult(
                deliveryId,
                true,
                ticket.Id,
                null,
                null,
                false,
                false);
        }

        var errorCode = ticket.Details?.Error;
        var classification = ExpoPushErrorClassifier.ClassifyTicketError(errorCode);

        return new ExpoPushSendResult(
            deliveryId,
            false,
            null,
            errorCode,
            ticket.Message,
            classification.IsPermanent,
            classification.IsRetryable);
    }

    private static ExpoPushReceiptResult MapReceiptResult(string ticketId, ExpoPushReceiptResponseItem receipt)
    {
        if (string.Equals(receipt.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return new ExpoPushReceiptResult(
                ticketId,
                true,
                null,
                null,
                false,
                false);
        }

        var errorCode = receipt.Details?.Error;
        var classification = ExpoPushErrorClassifier.ClassifyReceiptError(errorCode);

        return new ExpoPushReceiptResult(
            ticketId,
            false,
            errorCode,
            receipt.Message,
            classification.IsPermanent,
            classification.IsRetryable);
    }
}
