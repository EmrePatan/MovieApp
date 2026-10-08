using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Caching;

namespace MovieApp.UnitTests.Caching;

public sealed class PersonalizedCacheRebuildSchedulerTests
{
    [Fact]
    public void DebounceWindowIsInsideTheRequestedRange()
    {
        Assert.InRange(PersonalizedCacheRebuildScheduler.Debounce, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void ProductionRegistrationResolvesWithoutACustomDelay()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPersonalizedCacheRebuildScheduler, PersonalizedCacheRebuildScheduler>();
        using var provider = services.BuildServiceProvider();

        var scheduler = provider.GetRequiredService<IPersonalizedCacheRebuildScheduler>();

        Assert.IsType<PersonalizedCacheRebuildScheduler>(scheduler);
        scheduler.Schedule(Guid.NewGuid());
    }

    [Fact]
    public async Task ABurstOfBumpsRebuildsOnceAndALaterBurstRebuildsOnceMore()
    {
        var gate = new DebounceGate();
        var rebuilds = new GateRebuilder();
        var services = new ServiceCollection();
        services.AddSingleton<IPersonalizedCacheRebuilder>(rebuilds);
        await using var provider = services.BuildServiceProvider();
        using var scheduler = new PersonalizedCacheRebuildScheduler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PersonalizedCacheRebuildScheduler>.Instance,
            gate.Wait);

        var userId = Guid.NewGuid();
        for (var i = 0; i < 20; i++)
        {
            scheduler.Schedule(userId);
        }

        await gate.WaitUntilBlockedAsync();
        Assert.Equal(0, rebuilds.Count);

        gate.Release();
        await rebuilds.WaitUntilEnteredAsync(1);
        Assert.Equal(1, rebuilds.Count);

        for (var i = 0; i < 5; i++)
        {
            scheduler.Schedule(userId);
        }

        rebuilds.ReleaseOne();
        await gate.WaitUntilBlockedAsync();
        Assert.Equal(1, rebuilds.Count);

        gate.Release();
        await rebuilds.WaitUntilEnteredAsync(2);
        Assert.Equal(2, rebuilds.Count);
        rebuilds.ReleaseOne();
    }

    private sealed class DebounceGate
    {
        private readonly object _lock = new();
        private readonly Channel<bool> _blocked = Channel.CreateUnbounded<bool>();
        private TaskCompletionSource _current = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task Wait(CancellationToken cancellationToken)
        {
            Task task;
            lock (_lock)
            {
                task = _current.Task;
            }

            await _blocked.Writer.WriteAsync(true, cancellationToken);
            await task.WaitAsync(cancellationToken);
        }

        public async Task WaitUntilBlockedAsync() =>
            await _blocked.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public void Release()
        {
            TaskCompletionSource current;
            lock (_lock)
            {
                current = _current;
                _current = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            current.TrySetResult();
        }
    }

    private sealed class GateRebuilder : IPersonalizedCacheRebuilder
    {
        private readonly object _lock = new();
        private readonly List<TaskCompletionSource> _release = [];
        private int _released;
        private TaskCompletionSource<int>? _countWaiter;
        private int _countTarget;

        public int Count { get; private set; }

        public Task RebuildAsync(Guid userId, CancellationToken cancellationToken)
        {
            TaskCompletionSource release;
            lock (_lock)
            {
                Count++;
                release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _release.Add(release);
                if (_countWaiter is not null && Count >= _countTarget)
                {
                    _countWaiter.TrySetResult(Count);
                }
            }

            return release.Task.WaitAsync(cancellationToken);
        }

        public Task WaitUntilEnteredAsync(int count)
        {
            lock (_lock)
            {
                if (Count >= count)
                {
                    return Task.CompletedTask;
                }

                _countTarget = count;
                _countWaiter = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _countWaiter.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        public void ReleaseOne()
        {
            TaskCompletionSource release;
            lock (_lock)
            {
                release = _release[_released];
                _released++;
            }

            release.TrySetResult();
        }
    }
}
