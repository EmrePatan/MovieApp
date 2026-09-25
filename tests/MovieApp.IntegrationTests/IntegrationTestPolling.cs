namespace MovieApp.IntegrationTests;

internal static class IntegrationTestPolling
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(50);

    public static async Task<T> UntilAsync<T>(
        Func<Task<T?>> tryGet,
        Func<T, bool> satisfied,
        TimeSpan timeout,
        TimeSpan? interval = null)
    {
        var pollInterval = interval ?? DefaultInterval;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var result = await tryGet();
            if (result is not null && satisfied(result))
            {
                return result;
            }

            await Task.Delay(pollInterval);
        }

        throw new TimeoutException($"Condition was not met within {timeout.TotalSeconds:0.#}s.");
    }
}
