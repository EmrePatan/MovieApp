using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Catalog;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Catalog;

public sealed class CatalogGenreBackfillRetryPolicyTests
{
    [Fact]
    public void CalculateNextEligibleAtUtcUsesDifferentiatedIntervals()
    {
        var attemptedAt = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        var options = new CatalogGenreBackfillOptions
        {
            ProviderEmptyGenresRetryHours = 168,
            ProviderUnavailableRetryHours = 24,
            TransientFailureRetryMinutes = 15
        };

        var emptyGenres = CatalogGenreBackfillRetryPolicy
            .CalculateNextEligibleAtUtc(
                CatalogGenreRepairAttemptOutcome.ProviderNoUsableGenres,
                attemptedAt,
                options);
        var unavailable = CatalogGenreBackfillRetryPolicy
            .CalculateNextEligibleAtUtc(
                CatalogGenreRepairAttemptOutcome.ProviderUnavailable,
                attemptedAt,
                options);
        var transient = CatalogGenreBackfillRetryPolicy
            .CalculateNextEligibleAtUtc(
                CatalogGenreRepairAttemptOutcome.FailedTransient,
                attemptedAt,
                options);

        Assert.Equal(attemptedAt.AddDays(7), emptyGenres);
        Assert.Equal(attemptedAt.AddHours(24), unavailable);
        Assert.Equal(attemptedAt.AddMinutes(15), transient);
    }
}
