namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialReportResult(Guid Id, string TargetType, Guid TargetId, string Status, DateTime CreatedAt);

public interface ISocialReportService
{
    Task<SocialReportResult> CreateAsync(string targetType, Guid targetId, string reason, string? details,
        CancellationToken cancellationToken = default);
}