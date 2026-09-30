using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Keywords;

internal static class MdbListKeywordExternalReferenceEnsure
{
    internal static async Task<Guid> EnsureAsync(
        ApplicationDbContext dbContext,
        string externalId,
        Guid keywordId,
        string externalName,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        if (IsNpgsql(dbContext))
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO keyword_external_references ("Provider", "ExternalId", "KeywordId", "ExternalName", "CreatedAt")
                 VALUES ({KeywordProvider.MdbList.ToString()}, {externalId}, {keywordId}, {externalName}, {syncedAtUtc})
                 ON CONFLICT ("Provider", "ExternalId") DO NOTHING
                 """,
                cancellationToken);
        }
        else
        {
            var exists = await dbContext.KeywordExternalReferences
                .AsNoTracking()
                .AnyAsync(
                    reference =>
                        reference.Provider == KeywordProvider.MdbList &&
                        reference.ExternalId == externalId,
                    cancellationToken);

            if (!exists)
            {
                dbContext.KeywordExternalReferences.Add(new KeywordExternalReference
                {
                    Provider = KeywordProvider.MdbList,
                    ExternalId = externalId,
                    KeywordId = keywordId,
                    ExternalName = externalName,
                    CreatedAt = syncedAtUtc,
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var authoritative = await dbContext.KeywordExternalReferences
            .AsNoTracking()
            .FirstAsync(
                reference =>
                    reference.Provider == KeywordProvider.MdbList &&
                    reference.ExternalId == externalId,
                cancellationToken);

        return authoritative.KeywordId;
    }

    internal static string ToExternalId(int externalId) =>
        externalId.ToString(CultureInfo.InvariantCulture);

    private static bool IsNpgsql(ApplicationDbContext dbContext) =>
        dbContext.Database.IsRelational() &&
        dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;
}
