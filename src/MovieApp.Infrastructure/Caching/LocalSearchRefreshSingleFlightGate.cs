using System.Collections.Concurrent;

namespace MovieApp.Infrastructure.Caching;

/// <summary>
/// In-process single-flight gate used only when the distributed Redis lock is unavailable.
/// This is not a cross-instance lock and must not replace Redis when Redis is healthy.
/// </summary>
public sealed class LocalSearchRefreshSingleFlightGate
{
    private readonly ConcurrentDictionary<string, GateEntry> _gates = new(StringComparer.Ordinal);

    public bool TryAcquire(string lockKey, out string lockToken)
    {
        var entry = _gates.GetOrAdd(lockKey, static _ => new GateEntry());

        lock (entry.Sync)
        {
            if (entry.IsHeld)
            {
                lockToken = string.Empty;
                return false;
            }

            entry.IsHeld = true;
            entry.Token = Guid.NewGuid().ToString("N");
            lockToken = entry.Token;
            return true;
        }
    }

    public bool Release(string lockKey, string lockToken)
    {
        if (!_gates.TryGetValue(lockKey, out var entry))
        {
            return false;
        }

        lock (entry.Sync)
        {
            if (!entry.IsHeld || !string.Equals(entry.Token, lockToken, StringComparison.Ordinal))
            {
                return false;
            }

            entry.IsHeld = false;
            return true;
        }
    }

    public bool VerifyOwnership(string lockKey, string lockToken)
    {
        if (!_gates.TryGetValue(lockKey, out var entry))
        {
            return false;
        }

        lock (entry.Sync)
        {
            return entry.IsHeld && string.Equals(entry.Token, lockToken, StringComparison.Ordinal);
        }
    }

    private sealed class GateEntry
    {
        public object Sync { get; } = new();

        public bool IsHeld { get; set; }

        public string Token { get; set; } = string.Empty;
    }
}
