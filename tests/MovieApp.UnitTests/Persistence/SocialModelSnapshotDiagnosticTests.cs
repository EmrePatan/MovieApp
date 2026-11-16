using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using MovieApp.Infrastructure.Persistence;
using Xunit;

namespace MovieApp.UnitTests.Persistence;

public sealed class SocialModelSnapshotDiagnosticTests
{
    private static readonly string[] SocialTableNames =
    [
        "user_follows",
        "user_privacy_settings",
        "user_blocks",
        "social_activities",
        "social_activity_likes",
        "social_comments",
        "social_comment_likes",
        "social_notifications",
        "social_reports"
    ];

    [Fact]
    public void SnapshotHasNoSocialSchemaDifferences()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=snapshot_diagnostic;Username=postgres;Password=postgres")
            .Options;
        using var context = new ApplicationDbContext(options);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
        Assert.NotNull(snapshot);

        var differ = context.GetService<IMigrationsModelDiffer>();
        var initializedSnapshot = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var operations = differ.GetDifferences(initializedSnapshot.GetRelationalModel(), designTimeModel.GetRelationalModel());

        var socialOperations = operations.Where(operation =>
        {
            var values = operation.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Select(property =>
                {
                    try { return property.GetValue(operation)?.ToString(); }
                    catch { return null; }
                });
            return values.Any(value => value is not null
                && SocialTableNames.Any(table => value.Contains(table, StringComparison.OrdinalIgnoreCase)));
        }).ToArray();

        Assert.Empty(socialOperations);
    }
}
