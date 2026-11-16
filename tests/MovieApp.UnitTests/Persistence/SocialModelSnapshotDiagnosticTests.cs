using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using MovieApp.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace MovieApp.UnitTests.Persistence;

public sealed class SocialModelSnapshotDiagnosticTests(ITestOutputHelper output)
{
    [Fact]
    public void PrintModelSnapshotDifferences()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=snapshot_diagnostic;Username=postgres;Password=postgres")
            .Options;
        using var context = new ApplicationDbContext(options);
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var snapshot = migrationsAssembly.ModelSnapshot?.Model;
        Assert.NotNull(snapshot);

        var differ = context.GetService<IMigrationsModelDiffer>();
        var operations = differ.GetDifferences(snapshot.GetRelationalModel(), context.Model.GetRelationalModel());
        foreach (var operation in operations)
        {
            var values = operation.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Select(property =>
                {
                    try { return property.Name + "=" + property.GetValue(operation); }
                    catch { return property.Name + "=<unavailable>"; }
                });
            output.WriteLine(operation.GetType().Name + ": " + string.Join("; ", values));
        }

        Assert.Empty(operations);
    }
}
