namespace MovieApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL <c>uuid</c> order: unsigned comparison of the RFC 4122 network-order bytes.
/// Library keyset cursors use this so an in-memory page matches <c>uuid</c> comparison in SQL.
/// </summary>
internal sealed class PostgresGuidComparer : IComparer<Guid>
{
    internal static readonly PostgresGuidComparer Instance = new();

    public int Compare(Guid x, Guid y)
    {
        Span<byte> left = stackalloc byte[16];
        Span<byte> right = stackalloc byte[16];
        if (!x.TryWriteBytes(left) || !y.TryWriteBytes(right))
        {
            throw new InvalidOperationException("Unable to compare library identifiers.");
        }

        ToNetworkOrder(left);
        ToNetworkOrder(right);
        return left.SequenceCompareTo(right);
    }

    private static void ToNetworkOrder(Span<byte> bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }
}

/// <summary>
/// PostgreSQL <c>timestamp DESC</c> order, which places nulls first.
/// </summary>
internal sealed class PostgresTimestampDescComparer : IComparer<DateTime?>
{
    internal static readonly PostgresTimestampDescComparer Instance = new();

    public int Compare(DateTime? x, DateTime? y)
    {
        if (x is null || y is null)
        {
            return x is null ? (y is null ? 0 : -1) : 1;
        }

        return y.Value.CompareTo(x.Value);
    }
}
