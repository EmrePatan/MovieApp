namespace MovieApp.Api.BackgroundJobs;

public interface IRecurringBackgroundJobRegistrar
{
    void RegisterRecurringJobs();

    void RemoveAllRecurringJobs();
}
