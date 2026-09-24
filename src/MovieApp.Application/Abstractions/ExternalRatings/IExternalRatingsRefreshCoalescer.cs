namespace MovieApp.Application.Abstractions.ExternalRatings;

public interface IExternalRatingsRefreshCoalescer
{
    Task CoalesceAsync(string key, Func<Task> factory);
}
