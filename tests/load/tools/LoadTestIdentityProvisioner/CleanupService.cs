using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.LoadTestIdentityProvisioner;

public static class CleanupService
{
    public static async Task<CleanupResult> RunAsync(CleanupOptions options, CancellationToken cancellationToken = default)
    {
        var manifest = await Load60IdentityManifest.ReadAsync(options.ManifestPath, cancellationToken);
        using var provider = CreateScope(options.ConnectionString);
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var repository = provider.GetRequiredService<IUserRepository>();

        var targets = new List<CleanupTarget>();
        foreach (var entry in manifest.Identities)
        {
            if (!Load60IdentityFormats.IsDedicatedLoad60HarnessId(entry.HarnessId))
            {
                return CleanupResult.Refuse($"Harness id '{entry.HarnessId}' is not a dedicated LOAD60 identity.");
            }

            if (!Load60IdentityFormats.IsDedicatedLoad60NormalizedEmail(entry.NormalizedEmail, manifest.EmailDomain))
            {
                return CleanupResult.Refuse($"Manifest entry '{entry.HarnessId}' does not match LOAD60 email convention.");
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == entry.UserId, cancellationToken);
            if (user is null)
            {
                targets.Add(new CleanupTarget(entry, CleanupTargetStatus.Missing, "User not found (already deleted)."));
                continue;
            }

            if (!string.Equals(user.NormalizedEmail, entry.NormalizedEmail, StringComparison.Ordinal))
            {
                return CleanupResult.Refuse(
                    $"User {entry.UserId} email no longer matches manifest (refusing delete).");
            }

            if (!Load60IdentityFormats.IsDedicatedLoad60NormalizedEmail(user.NormalizedEmail, manifest.EmailDomain))
            {
                return CleanupResult.Refuse($"User {entry.UserId} is not a dedicated LOAD60 identity.");
            }

            targets.Add(new CleanupTarget(entry, CleanupTargetStatus.Eligible, null));
        }

        if (options.DryRun)
        {
            return CleanupResult.DryRun(targets);
        }

        if (!options.ConfirmDelete)
        {
            throw new InvalidOperationException("Destructive cleanup requires --confirm-delete.");
        }

        var deleted = 0;
        var context = provider.GetRequiredService<IApplicationDbContext>();
        await context.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var target in targets.Where(t => t.Status == CleanupTargetStatus.Eligible))
            {
                var removed = await repository.DeleteAsync(target.Entry.UserId, ct);
                if (removed)
                {
                    deleted++;
                }
            }
        }, cancellationToken);

        return CleanupResult.Succeeded(targets, deleted);
    }

    private static ServiceProvider CreateScope(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        return services.BuildServiceProvider();
    }
}

public sealed class CleanupOptions
{
    public required string ConnectionString { get; init; }

    public required string ManifestPath { get; init; }

    public bool DryRun { get; init; } = true;

    public bool ConfirmDelete { get; init; }
}

public enum CleanupTargetStatus
{
    Eligible,
    Missing,
}

public sealed record CleanupTarget(
    Load60IdentityManifestEntry Entry,
    CleanupTargetStatus Status,
    string? Detail);

public sealed record CleanupResult(
    bool IsDryRun,
    bool Refused,
    IReadOnlyList<CleanupTarget>? Targets,
    int DeletedCount,
    string Message)
{
    public static CleanupResult DryRun(IReadOnlyList<CleanupTarget> targets) =>
        new(true, false, targets, 0, "Cleanup dry run complete.");

    public static CleanupResult Refuse(string message) =>
        new(false, true, null, 0, message);

    public static CleanupResult Succeeded(IReadOnlyList<CleanupTarget> targets, int deleted) =>
        new(false, false, targets, deleted, $"Deleted {deleted} LOAD60 user(s).");
}
