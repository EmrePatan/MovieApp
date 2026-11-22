using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.UnitTests.Persistence;

public sealed class ApplicationDbContextModelTests
{
    /// <summary>
    /// Compares the EF model with the checked-in migration snapshot without opening a database connection.
    /// Fails when an entity or configuration changed but no migration (and snapshot update) was added.
    /// </summary>
    [Fact]
    public void ModelHasNoPendingChangesComparedToMigrationSnapshot()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_check;Username=model_check;Password=unused")
            .Options;

        using var context = new ApplicationDbContext(options);

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
