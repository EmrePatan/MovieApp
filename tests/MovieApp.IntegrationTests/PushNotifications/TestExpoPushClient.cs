using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Models.PushNotifications;

namespace MovieApp.IntegrationTests.PushNotifications;

public sealed class TestExpoPushClient : IExpoPushClient
{
    private readonly object _sync = new();
    private Func<Guid, ExpoPushSendResult>? _defaultSendHandler;
    private readonly Dictionary<string, ExpoPushReceiptResult> _receipts = [];

    public int SendCallCount { get; private set; }

    public IReadOnlyList<PushNotificationMessage> LastSentMessages { get; private set; } = [];

    public void ConfigureSendSuccess(string ticketId = "ticket-success")
    {
        lock (_sync)
        {
            _defaultSendHandler = _ => new ExpoPushSendResult(
                Guid.Empty,
                true,
                ticketId,
                null,
                null,
                false,
                false);
        }
    }

    public void ConfigureSendError(string errorCode, bool permanent)
    {
        lock (_sync)
        {
            _defaultSendHandler = _ => new ExpoPushSendResult(
                Guid.Empty,
                false,
                null,
                errorCode,
                errorCode,
                permanent,
                !permanent);
        }
    }

    public void ConfigureReceipt(string ticketId, ExpoPushReceiptResult receipt)
    {
        lock (_sync)
        {
            _receipts[ticketId] = receipt;
        }
    }

    public Task<IReadOnlyList<ExpoPushSendResult>> SendAsync(
        IReadOnlyList<PushNotificationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            SendCallCount++;
            LastSentMessages = messages.ToList();
        }

        var results = messages
            .Select(message =>
            {
                lock (_sync)
                {
                    if (_defaultSendHandler is not null)
                    {
                        var result = _defaultSendHandler(message.DeliveryId);
                        return result with { DeliveryId = message.DeliveryId };
                    }

                    return new ExpoPushSendResult(
                        message.DeliveryId,
                        true,
                        $"ticket-{message.DeliveryId:N}",
                        null,
                        null,
                        false,
                        false);
                }
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ExpoPushSendResult>>(results);
    }

    public Task<IReadOnlyList<ExpoPushReceiptResult>> GetReceiptsAsync(
        IReadOnlyCollection<string> ticketIds,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var results = ticketIds
                .Where(ticketId => _receipts.ContainsKey(ticketId))
                .Select(ticketId => _receipts[ticketId])
                .ToList();

            return Task.FromResult<IReadOnlyList<ExpoPushReceiptResult>>(results);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            SendCallCount = 0;
            LastSentMessages = [];
            _defaultSendHandler = null;
            _receipts.Clear();
        }
    }
}
