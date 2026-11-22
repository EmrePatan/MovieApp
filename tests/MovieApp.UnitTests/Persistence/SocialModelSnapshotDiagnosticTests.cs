using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using MovieApp.Infrastructure.Persistence;
using Xunit;

namespace MovieApp.UnitTests.Persistence;

public sealed class SocialModelSnapshotDiagnosticTests
{
    [Fact]
    public void ModelSnapshotHasNoPendingChanges()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=snapshot_diagnostic;Username=postgres;Password=postgres")
            .Options;
        using var context = new ApplicationDbContext(options);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
        Assert.NotNull(snapshot);

        var initializedSnapshot = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var operations = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(initializedSnapshot.GetRelationalModel(), designTimeModel.GetRelationalModel());

        Assert.Empty(operations);
    }
}
