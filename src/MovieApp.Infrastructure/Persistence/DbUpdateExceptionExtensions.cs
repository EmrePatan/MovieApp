using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MovieApp.Infrastructure.Persistence;

internal static class DbUpdateExceptionExtensions
{
    internal static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException &&
        postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
}
