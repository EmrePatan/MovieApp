namespace MovieApp.Domain.Entities;

public sealed class SocialReport
{
    public Guid Id { get; set; }
    public Guid ReporterId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public static SocialReport Create(Guid reporterId, string targetType, Guid targetId, string reason, string? details, DateTime utcNow)
    {
        if (reporterId == Guid.Empty || targetId == Guid.Empty)
            throw new ArgumentException("Reporter and target identifiers are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 64) throw new ArgumentOutOfRangeException(nameof(reason));
        var normalizedDetails = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        if (normalizedDetails?.Length > 1000) throw new ArgumentOutOfRangeException(nameof(details));
        return new SocialReport
        {
            Id = Guid.NewGuid(), ReporterId = reporterId, TargetType = targetType,
            TargetId = targetId, Reason = normalizedReason, Details = normalizedDetails,
            CreatedAt = utcNow, Status = "Open"
        };
    }
}
