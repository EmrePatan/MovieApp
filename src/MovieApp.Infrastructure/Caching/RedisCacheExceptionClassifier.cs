namespace MovieApp.Infrastructure.Caching;

internal static class RedisCacheExceptionClassifier
{
    internal static bool IsRedisInfrastructureFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException)
            {
                return true;
            }

            var typeName = current.GetType().FullName ?? current.GetType().Name;

            if (typeName.StartsWith("StackExchange.Redis.", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
