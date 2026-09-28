using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MovieApp.Infrastructure.Persistence;

internal static class DbUpdateExceptionExtensions
{
    internal static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException &&
        postgresException.SqlState == PostgresErrorCodes.UniqueViolation;

    internal static bool IsUniqueConstraintViolation(
        DbUpdateException exception,
        string constraintName) =>
        exception.InnerException is PostgresException postgresException &&
        postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
        string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal);
}
