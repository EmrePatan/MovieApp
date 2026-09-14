namespace MovieApp.Api.BackgroundJobs;

public sealed class NoOpRecurringBackgroundJobRegistrar : IRecurringBackgroundJobRegistrar
{
    public void RegisterRecurringJobs()
    {
    }

    public void RemoveAllRecurringJobs()
    {
    }
}
