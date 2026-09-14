using System.Linq.Expressions;
using Hangfire;
using Hangfire.Common;

namespace MovieApp.UnitTests.BackgroundJobs;

internal sealed class FakeRecurringJobManager : IRecurringJobManager
{
    public List<(string JobId, string Cron)> AddedOrUpdated { get; } = [];

    public List<string> Removed { get; } = [];

    public void AddOrUpdate(
        string recurringJobId,
        Job job,
        string cronExpression,
        RecurringJobOptions? options = null) =>
        Upsert(recurringJobId, cronExpression);

    public void AddOrUpdate<T>(string recurringJobId, Expression<Action<T>> methodCall, string cronExpression) =>
        Upsert(recurringJobId, cronExpression);

    public void AddOrUpdate<T>(
        string recurringJobId,
        Expression<Action<T>> methodCall,
        string cronExpression,
        RecurringJobOptions options) =>
        Upsert(recurringJobId, cronExpression);

    public void AddOrUpdate<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression) =>
        Upsert(recurringJobId, cronExpression);

    public void AddOrUpdate<T>(
        string recurringJobId,
        Expression<Func<T, Task>> methodCall,
        string cronExpression,
        RecurringJobOptions options) =>
        Upsert(recurringJobId, cronExpression);

    private void Upsert(string recurringJobId, string cronExpression)
    {
        var existingIndex = AddedOrUpdated.FindIndex(entry => entry.JobId == recurringJobId);
        if (existingIndex >= 0)
        {
            AddedOrUpdated[existingIndex] = (recurringJobId, cronExpression);
            return;
        }

        AddedOrUpdated.Add((recurringJobId, cronExpression));
    }

    public void RemoveIfExists(string recurringJobId) => Removed.Add(recurringJobId);

    public void Trigger(string recurringJobId) =>
        throw new NotSupportedException();

    public void TriggerJob(string recurringJobId) =>
        throw new NotSupportedException();
}
