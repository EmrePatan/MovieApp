using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class KeywordLocalizationMachineUpsertSql
{
    private static readonly int MachineSource = (int)KeywordTranslationSource.Machine;
    private static readonly int UnreviewedStatus = (int)KeywordTranslationReviewStatus.Unreviewed;
    private static readonly int CuratedSource = (int)KeywordTranslationSource.Curated;
    private static readonly int ReviewedStatus = (int)KeywordTranslationReviewStatus.Reviewed;

    internal sealed record UpsertRow(Guid KeywordId, bool Inserted);

    public static async Task<KeywordLocalizationMachineUpsertResult> ExecuteBatchAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<KeywordLocalizationMachineUpsert> upserts,
        CancellationToken cancellationToken)
    {
        var inserted = 0;
        var updated = 0;
        var skippedProtected = 0;

        foreach (var upsert in upserts)
        {
            var rows = await dbContext.Database
                .SqlQuery<UpsertRow>($"""
                    INSERT INTO keyword_localizations (
                        "KeywordId",
                        "Locale",
                        "Name",
                        "NormalizedName",
                        "TranslationSource",
                        "ReviewStatus",
                        "SourceTextHash",
                        "CreatedAtUtc",
                        "UpdatedAtUtc")
                    VALUES (
                        {upsert.KeywordId},
                        {upsert.Locale},
                        {upsert.Name},
                        {upsert.NormalizedName},
                        {MachineSource},
                        {UnreviewedStatus},
                        {upsert.SourceTextHash},
                        {upsert.UtcNow},
                        {upsert.UtcNow})
                    ON CONFLICT ("KeywordId", "Locale") DO UPDATE SET
                        "Name" = EXCLUDED."Name",
                        "NormalizedName" = EXCLUDED."NormalizedName",
                        "TranslationSource" = EXCLUDED."TranslationSource",
                        "ReviewStatus" = EXCLUDED."ReviewStatus",
                        "SourceTextHash" = EXCLUDED."SourceTextHash",
                        "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
                    WHERE keyword_localizations."TranslationSource" <> {CuratedSource}
                      AND keyword_localizations."ReviewStatus" <> {ReviewedStatus}
                    RETURNING "KeywordId", (xmax = 0) AS "Inserted"
                    """)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                skippedProtected++;
                continue;
            }

            if (rows[0].Inserted)
            {
                inserted++;
            }
            else
            {
                updated++;
            }
        }

        return new KeywordLocalizationMachineUpsertResult(inserted, updated, skippedProtected);
    }
}
