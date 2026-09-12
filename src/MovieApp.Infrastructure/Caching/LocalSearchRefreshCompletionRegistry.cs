using System.Collections.Concurrent;
using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.Infrastructure.Caching;

/// <summary>
/// In-process refresh completion registry used when Redis is unavailable.
/// Not cross-instance and must not replace Redis when Redis is healthy.
/// </summary>
public sealed class LocalSearchRefreshCompletionRegistry
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public void Publish(string completionKey, SearchRefreshAttemptOutcome outcome, TimeSpan ttl)
    {
        _entries[completionKey] = new Entry(outcome, DateTime.UtcNow.Add(ttl));
    }

    public SearchRefreshAttemptOutcome? TryGet(string completionKey)
    {
        if (!_entries.TryGetValue(completionKey, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _entries.TryRemove(completionKey, out _);
            return null;
        }

        return entry.Outcome;
    }

    private sealed record Entry(SearchRefreshAttemptOutcome Outcome, DateTime ExpiresAtUtc);
}
