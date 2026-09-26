#pragma warning disable EF1001

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class SearchPostgresFunctionMapping
{
    internal static void MapSearchPostgresFunctions(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(
                typeof(SearchPostgresFunctions).GetMethod(
                    nameof(SearchPostgresFunctions.Least),
                    [typeof(int), typeof(int)])!)
            .HasTranslation(static args =>
                new SqlFunctionExpression(
                    null,
                    null,
                    "LEAST",
                    args,
                    nullable: false,
                    instancePropagatesNullability: false,
                    argumentsPropagateNullability: args.Select(_ => false),
                    builtIn: true,
                    typeof(int),
                    null));

        modelBuilder.HasDbFunction(
                typeof(SearchPostgresFunctions).GetMethod(
                    nameof(SearchPostgresFunctions.Least),
                    [typeof(int), typeof(int), typeof(int)])!)
            .HasTranslation(static args =>
                new SqlFunctionExpression(
                    null,
                    null,
                    "LEAST",
                    args,
                    nullable: false,
                    instancePropagatesNullability: false,
                    argumentsPropagateNullability: args.Select(_ => false),
                    builtIn: true,
                    typeof(int),
                    null));
    }
}

#pragma warning restore EF1001
