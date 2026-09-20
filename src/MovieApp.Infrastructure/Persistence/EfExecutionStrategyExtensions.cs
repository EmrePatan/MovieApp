using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace MovieApp.Infrastructure.Persistence;

internal static class EfExecutionStrategyExtensions
{
    public static async Task ExecuteInRetriableTransactionAsync(
        this DatabaseFacade database,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await database.BeginTransactionAsync(cancellationToken);

            try
            {
                await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public static async Task<TResult> ExecuteInRetriableTransactionAsync<TResult>(
        this DatabaseFacade database,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
