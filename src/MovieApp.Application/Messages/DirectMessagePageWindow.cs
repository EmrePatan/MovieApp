namespace MovieApp.Application.Messages;

public static class DirectMessagePageWindow
{
    public static (int Skip, int Take, bool HasMore) Resolve(int totalCount, int page, int pageSize)
    {
        if (totalCount <= 0 || page <= 0 || pageSize <= 0)
        {
            return (0, 0, false);
        }

        var offsetFromEnd = (page - 1) * pageSize;
        if (offsetFromEnd >= totalCount)
        {
            return (0, 0, false);
        }

        var take = Math.Min(pageSize, totalCount - offsetFromEnd);
        var skip = totalCount - offsetFromEnd - take;
        return (skip, take, skip > 0);
    }
}
