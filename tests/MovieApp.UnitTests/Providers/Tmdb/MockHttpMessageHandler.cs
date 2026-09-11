namespace MovieApp.UnitTests.Providers.Tmdb;

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _handlers = new();

    public IList<HttpRequestMessage> Requests { get; } = [];

    public void EnqueueResponse(HttpResponseMessage response)
    {
        _handlers.Enqueue((_, _) => Task.FromResult(response));
    }

    public void EnqueueResponse(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _handlers.Enqueue((request, _) => Task.FromResult(responseFactory(request)));
    }

    public void EnqueueException(Exception exception)
    {
        _handlers.Enqueue((_, _) => throw exception);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (_handlers.Count == 0)
        {
            throw new InvalidOperationException("No mock HTTP response was configured.");
        }

        return _handlers.Dequeue()(request, cancellationToken);
    }
}
