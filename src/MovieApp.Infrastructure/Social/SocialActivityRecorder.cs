using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialActivityRecorder(ApplicationDbContext dbContext) : ISocialActivityRecorder
{
    public async Task RecordAsync(
        Guid actorId,
        SocialActivityType activityType,
        Guid sourceEntityId,
        Guid targetContentId,
        string targetContentType,
        DateTime createdAt,
        CancellationToken cancellationToken = default)
    {
        var activity = SocialActivity.Create(
            actorId, activityType, sourceEntityId, targetContentId, targetContentType, createdAt);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO social_activities
                ("Id", "ActorId", "ActivityType", "SourceEntityId", "TargetContentId",
                 "TargetContentType", "CreatedAt", "IsDeleted")
            VALUES
                ({activity.Id}, {actorId}, {activityType.ToString()}, {sourceEntityId},
                 {targetContentId}, {targetContentType}, {createdAt}, FALSE)
            ON CONFLICT ("ActorId", "ActivityType", "SourceEntityId") DO UPDATE
            SET "TargetContentId" = EXCLUDED."TargetContentId",
                "TargetContentType" = EXCLUDED."TargetContentType",
                "CreatedAt" = EXCLUDED."CreatedAt",
                "IsDeleted" = FALSE;
            """, cancellationToken);
    }
    public async Task RetractAsync(
        Guid actorId,
        SocialActivityType activityType,
        Guid targetContentId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE social_activities
            SET "IsDeleted" = TRUE
            WHERE "ActorId" = {actorId}
              AND "ActivityType" = {activityType.ToString()}
              AND "TargetContentId" = {targetContentId}
              AND "IsDeleted" = FALSE;
            """, cancellationToken);
    }
}
