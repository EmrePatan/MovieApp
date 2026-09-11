using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal static class ExternalIdIndexExtensions
{
    internal static IndexBuilder<TEntity> AsUniqueExternalIdIndex<TEntity>(
        this IndexBuilder<TEntity> indexBuilder,
        string columnName)
        where TEntity : class
    {
        return indexBuilder
            .IsUnique()
            .HasFilter($"\"{columnName}\" IS NOT NULL");
    }
}
