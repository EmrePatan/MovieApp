using System.Threading.RateLimiting;
using MovieApp.Application.Abstractions.RateLimiting;

namespace MovieApp.Api.RateLimiting;

internal sealed class DistributedFixedWindowRateLimiter : RateLimiter
{
    private readonly IRateLimitCounterStore _store;
    private readonly string _partitionKey;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;

    public DistributedFixedWindowRateLimiter(
        IRateLimitCounterStore store,
        string partitionKey,
        int permitLimit,
        TimeSpan window)
    {
        _store = store;
        _partitionKey = partitionKey;
        _permitLimit = Math.Max(1, permitLimit);
        _window = window <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : window;
    }

    public override RateLimiterStatistics? GetStatistics() => null;

    public override TimeSpan? IdleDuration => null;

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken)
    {
        if (permitCount != 1)
        {
            throw new NotSupportedException("Only single-permit acquisitions are supported.");
        }

        return new ValueTask<RateLimitLease>(AcquireSingleAsync(cancellationToken));
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        if (permitCount != 1)
        {
            throw new NotSupportedException("Only single-permit acquisitions are supported.");
        }

        return AcquireSingleAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return base.DisposeAsyncCore();
    }

    private async Task<RateLimitLease> AcquireSingleAsync(CancellationToken cancellationToken)
    {
        var result = await _store.TryAcquireAsync(
            _partitionKey,
            _permitLimit,
            _window,
            cancellationToken);

        if (result.IsAcquired)
        {
            return new DistributedRateLimitLease(isAcquired: true);
        }

        return new DistributedRateLimitLease(
            isAcquired: false,
            retryAfter: result.RetryAfter ?? _window);
    }
}

internal sealed class DistributedRateLimitLease : RateLimitLease
{
    private static readonly string[] AllMetadataNames = [MetadataName.RetryAfter.Name];

    private readonly bool _isAcquired;
    private readonly TimeSpan? _retryAfter;

    public DistributedRateLimitLease(bool isAcquired, TimeSpan? retryAfter = null)
    {
        _isAcquired = isAcquired;
        _retryAfter = retryAfter;
    }

    public override bool IsAcquired => _isAcquired;

    public override IEnumerable<string> MetadataNames =>
        _retryAfter.HasValue ? AllMetadataNames : [];

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (_retryAfter.HasValue &&
            string.Equals(metadataName, MetadataName.RetryAfter.Name, StringComparison.Ordinal))
        {
            metadata = _retryAfter.Value;
            return true;
        }

        metadata = null;
        return false;
    }
}
