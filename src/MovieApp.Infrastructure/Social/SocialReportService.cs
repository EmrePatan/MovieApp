using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialReportService(ApplicationDbContext dbContext, ICurrentUser currentUser) : ISocialReportService
{
    public async Task<SocialReportResult> CreateAsync(string targetType, Guid targetId, string reason, string? details,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid reporterId || reporterId == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");
        if (targetId == Guid.Empty) throw new ValidationException("A valid report target is required.");

        var normalizedType = targetType?.Trim().ToLowerInvariant() switch
        {
            "user" => "User",
            "activity" => "Activity",
            "comment" => "Comment",
            _ => throw new ValidationException("Unsupported report target type.")
        };

        var targetExists = normalizedType switch
        {
            "User" => await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == targetId && user.IsActive, cancellationToken),
            "Activity" => await dbContext.SocialActivities.AsNoTracking().AnyAsync(activity => activity.Id == targetId && !activity.IsDeleted, cancellationToken),
            "Comment" => await dbContext.SocialComments.AsNoTracking().AnyAsync(comment => comment.Id == targetId && !comment.IsDeleted, cancellationToken),
            _ => false
        };
        if (!targetExists) throw new NotFoundException("Report target not found.");
        if (normalizedType == "User" && targetId == reporterId)
            throw new ConflictException("You cannot report yourself.");

        var recentDuplicate = await dbContext.SocialReports.AsNoTracking().AnyAsync(report =>
            report.ReporterId == reporterId && report.TargetType == normalizedType && report.TargetId == targetId
            && report.CreatedAt >= DateTime.UtcNow.AddDays(-1), cancellationToken);
        if (recentDuplicate) throw new ConflictException("You have already reported this target recently.");

        var report = SocialReport.Create(reporterId, normalizedType, targetId, reason, details, DateTime.UtcNow);
        dbContext.SocialReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SocialReportResult(report.Id, report.TargetType, report.TargetId, report.Status, report.CreatedAt);
    }
}