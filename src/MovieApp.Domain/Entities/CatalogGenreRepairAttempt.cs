using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class CatalogGenreRepairAttempt
{
    public Guid Id { get; set; }

    public Guid CatalogId { get; set; }

    public CatalogContentType ContentType { get; set; }

    public DateTime LastAttemptAtUtc { get; set; }

    public CatalogGenreRepairAttemptOutcome LastOutcome { get; set; }

    public DateTime NextEligibleAtUtc { get; set; }
}
