using MovieApp.Application.Configuration;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Catalog;

public static class CatalogGenreBackfillRetryPolicy
{
    public static DateTime CalculateNextEligibleAtUtc(
        CatalogGenreRepairAttemptOutcome outcome,
        DateTime attemptedAtUtc,
        CatalogGenreBackfillOptions options) =>
        outcome switch
        {
            CatalogGenreRepairAttemptOutcome.ProviderNoUsableGenres => attemptedAtUtc.AddHours(
                Math.Max(1, options.ProviderEmptyGenresRetryHours)),
            CatalogGenreRepairAttemptOutcome.ProviderUnavailable => attemptedAtUtc.AddHours(
                Math.Max(1, options.ProviderUnavailableRetryHours)),
            CatalogGenreRepairAttemptOutcome.FailedTransient => attemptedAtUtc.AddMinutes(
                Math.Max(1, options.TransientFailureRetryMinutes)),
            _ => attemptedAtUtc
        };
}
