using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Identity;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.LoadTestIdentityProvisioner;

public static class ProvisionerService
{
    public static async Task<ProvisionResult> RunAsync(ProvisionOptions options, CancellationToken cancellationToken = default)
    {
        var slots = Load60IdentityFormats.BuildSlots(options.Count, options.EmailDomain);
        using var provider = CreateScope(options.ConnectionString);
        var db = provider.GetRequiredService<ApplicationDbContext>();

        var normalizedEmails = slots.Select(s => s.NormalizedEmail).ToList();
        var existingUsers = await db.Users
            .Where(u => normalizedEmails.Contains(u.NormalizedEmail))
            .ToListAsync(cancellationToken);
        var existingByEmail = existingUsers.ToDictionary(u => u.NormalizedEmail, StringComparer.Ordinal);

        var analysis = Load60CollisionAnalyzer.Analyze(slots, existingByEmail, options.EmailDomain);
        if (!analysis.CanWrite)
        {
            return ProvisionResult.Blocked(analysis, "Collision with non-LOAD60 users.");
        }

        options.EnsureWriteAuthorized();

        if (options.DryRun)
        {
            return ProvisionResult.DryRun(analysis);
        }

        var password = SecurePasswordPrompt.ReadCampaignPassword("Campaign password (not stored): ");
        try
        {
            var hasher = provider.GetRequiredService<IPasswordHasher>();
            var passwordHash = hasher.HashPassword(password);
            var repository = provider.GetRequiredService<IUserRepository>();
            var context = provider.GetRequiredService<IApplicationDbContext>();
            var createdEntries = new List<Load60IdentityManifestEntry>();
            var utcNow = DateTime.UtcNow;
            var createdCount = 0;

            await context.ExecuteInTransactionAsync(async ct =>
            {
                foreach (var plan in analysis.Slots)
                {
                    switch (plan.Status)
                    {
                        case Load60SlotStatus.ExistingDedicated:
                        {
                            var existing = existingByEmail[plan.Slot.NormalizedEmail];
                            createdEntries.Add(ToManifestEntry(plan.Slot, existing, existing.CreatedAt));
                            break;
                        }
                        case Load60SlotStatus.EligibleForCreation:
                        {
                            var user = User.Create(
                                Guid.NewGuid(),
                                plan.Slot.Email,
                                passwordHash,
                                plan.Slot.DisplayName,
                                utcNow);
                            user.MarkEmailVerified(utcNow);
                            await repository.CreateAsync(user, ct);
                            createdEntries.Add(ToManifestEntry(plan.Slot, user, utcNow));
                            createdCount++;
                            break;
                        }
                        default:
                            throw new InvalidOperationException($"Unexpected slot status: {plan.Status}");
                    }
                }
            }, cancellationToken);

            var manifest = new Load60IdentityManifest
            {
                Campaign = "load60",
                EmailDomain = options.EmailDomain.Trim().ToLowerInvariant(),
                Identities = createdEntries.OrderBy(e => e.HarnessId, StringComparer.Ordinal).ToList(),
            };

            await manifest.WriteAsync(options.ManifestPath, cancellationToken);
            return ProvisionResult.Succeeded(analysis, manifest, createdCount);
        }
        finally
        {
            password = string.Empty;
        }
    }

    private static Load60IdentityManifestEntry ToManifestEntry(Load60IdentitySlot slot, User user, DateTime createdAtUtc) =>
        new()
        {
            HarnessId = slot.HarnessId,
            UserId = user.Id,
            Email = user.Email,
            NormalizedEmail = user.NormalizedEmail,
            CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : createdAtUtc.ToUniversalTime(),
        };

    private static ServiceProvider CreateScope(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        return services.BuildServiceProvider();
    }
}

public sealed class ProvisionOptions
{
    public required string ConnectionString { get; init; }

    public int Count { get; init; } = LoadTestStageTokenRequirements.DefaultLoad60PoolSize;

    public string EmailDomain { get; init; } = Load60IdentityFormats.DefaultEmailDomain;

    public bool DryRun { get; init; } = true;

    public bool ConfirmProduction { get; init; }

    public required string ManifestPath { get; init; }

    public void EnsureWriteAuthorized()
    {
        if (!DryRun && !ConfirmProduction)
        {
            throw new InvalidOperationException("Production writes require --confirm-production.");
        }
    }
}

public sealed record ProvisionResult(
    Load60CollisionAnalysis Analysis,
    bool IsDryRun,
    bool IsBlocked,
    Load60IdentityManifest? Manifest,
    int CreatedCount,
    string Message)
{
    public static ProvisionResult DryRun(Load60CollisionAnalysis analysis) =>
        new(analysis, true, false, null, 0, "Dry run complete.");

    public static ProvisionResult Blocked(Load60CollisionAnalysis analysis, string message) =>
        new(analysis, false, true, null, 0, message);

    public static ProvisionResult Succeeded(Load60CollisionAnalysis analysis, Load60IdentityManifest manifest, int created) =>
        new(analysis, false, false, manifest, created, "Provision complete.");
}
