using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Caching;

/// <summary>
/// One in-flight rebuild per user. The first bump arms a <see cref="Debounce"/> wait;
/// further bumps in that window, including a bulk import, only mark the user dirty.
/// A rebuild that is already running does not start another until it finishes, and
/// then only if a newer bump arrived. The read path still keys on the generation it
/// observed, so a payload built for an older generation is never stored or returned.
/// </summary>
public sealed class PersonalizedCacheRebuildScheduler : IPersonalizedCacheRebuildScheduler, IDisposable
{
    public static readonly TimeSpan Debounce = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PersonalizedCacheRebuildScheduler> _logger;
    private readonly Func<CancellationToken, Task> _delay;
    private readonly CancellationTokenSource _stopped = new();
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Slot> _slots = [];

    public PersonalizedCacheRebuildScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<PersonalizedCacheRebuildScheduler> logger,
        Func<CancellationToken, Task>? delay = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _delay = delay ?? (cancellationToken => Task.Delay(Debounce, cancellationToken));
    }

    public void Schedule(Guid userId)
    {
        if (_stopped.IsCancellationRequested)
        {
            return;
        }

        var start = false;
        lock (_gate)
        {
            if (!_slots.TryGetValue(userId, out var slot))
            {
                slot = new Slot();
                _slots[userId] = slot;
            }

            slot.Dirty = true;
            if (!slot.Armed && !slot.Running)
            {
                slot.Armed = true;
                start = true;
            }
        }

        if (start)
        {
            _ = Task.Run(() => RunAsync(userId), CancellationToken.None);
        }
    }

    public void Dispose() => _stopped.Cancel();

    private async Task RunAsync(Guid userId)
    {
        try
        {
            await _delay(_stopped.Token);
        }
        catch (OperationCanceledException)
        {
            ClearArmed(userId);
            return;
        }
        catch (Exception exception)
        {
            PersonalizedCacheRebuildLogMessages.LogRebuildFailed(_logger, userId, exception);
            ClearArmed(userId);
            return;
        }

        if (_stopped.IsCancellationRequested)
        {
            ClearArmed(userId);
            return;
        }

        lock (_gate)
        {
            if (!_slots.TryGetValue(userId, out var slot))
            {
                return;
            }

            slot.Armed = false;
            slot.Running = true;
            slot.Dirty = false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rebuilder = scope.ServiceProvider.GetRequiredService<IPersonalizedCacheRebuilder>();
            await rebuilder.RebuildAsync(userId, _stopped.Token);
        }
        catch (OperationCanceledException) when (_stopped.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            PersonalizedCacheRebuildLogMessages.LogRebuildFailed(_logger, userId, exception);
        }

        var again = false;
        lock (_gate)
        {
            if (!_slots.TryGetValue(userId, out var slot))
            {
                return;
            }

            slot.Running = false;
            if (slot.Dirty && !_stopped.IsCancellationRequested)
            {
                slot.Armed = true;
                again = true;
            }
            else
            {
                _slots.Remove(userId);
            }
        }

        if (again)
        {
            await RunAsync(userId);
        }
    }

    private void ClearArmed(Guid userId)
    {
        lock (_gate)
        {
            if (!_slots.TryGetValue(userId, out var slot))
            {
                return;
            }

            slot.Armed = false;
            if (!slot.Running)
            {
                _slots.Remove(userId);
            }
        }
    }

    private sealed class Slot
    {
        public bool Dirty { get; set; }

        public bool Armed { get; set; }

        public bool Running { get; set; }
    }
}
