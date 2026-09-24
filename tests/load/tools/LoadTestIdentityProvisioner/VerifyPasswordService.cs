using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Identity;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.LoadTestIdentityProvisioner;

public sealed record VerifyPasswordSampleResult(
    string HarnessId,
    bool IsActive,
    bool IsEmailVerified,
    bool PasswordMatch,
    bool UserFound);

public sealed record VerifyPasswordRunResult(
    IReadOnlyList<VerifyPasswordSampleResult> Samples,
    bool AllMatch,
    bool AnyMismatch);

public static class VerifyPasswordService
{
    public static async Task<VerifyPasswordRunResult> RunAsync(
        VerifyPasswordOptions options,
        CancellationToken cancellationToken = default)
    {
        var manifest = await Load60IdentityManifest.ReadAsync(options.ManifestPath, cancellationToken);
        var samples = manifest.Identities
            .OrderBy(i => i.HarnessId, StringComparer.Ordinal)
            .Take(options.SampleSize)
            .ToList();

        if (samples.Count == 0)
        {
            throw new InvalidOperationException("No manifest identities to verify.");
        }

        foreach (var entry in samples)
        {
            if (!Load60IdentityFormats.IsDedicatedLoad60HarnessId(entry.HarnessId) ||
                !Load60IdentityFormats.IsDedicatedLoad60NormalizedEmail(entry.NormalizedEmail, manifest.EmailDomain))
            {
                throw new InvalidOperationException($"Manifest entry '{entry.HarnessId}' is not a dedicated LOAD60 identity.");
            }
        }

        var password = SecurePasswordPrompt.ReadCampaignPassword("Campaign password (read-only verify): ");
        try
        {
            using var provider = CreateScope(options.ConnectionString);
            var db = provider.GetRequiredService<ApplicationDbContext>();
            var hasher = provider.GetRequiredService<IPasswordHasher>();

            var results = new List<VerifyPasswordSampleResult>(samples.Count);
            foreach (var entry in samples)
            {
                var user = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == entry.UserId, cancellationToken);

                if (user is null)
                {
                    results.Add(new VerifyPasswordSampleResult(
                        entry.HarnessId,
                        false,
                        false,
                        false,
                        UserFound: false));
                    continue;
                }

                if (!string.Equals(user.NormalizedEmail, entry.NormalizedEmail, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"User {entry.HarnessId} normalized email does not match manifest.");
                }

                var passwordMatch = user.PasswordHash is not null &&
                                    hasher.VerifyPassword(password, user.PasswordHash);

                results.Add(new VerifyPasswordSampleResult(
                    entry.HarnessId,
                    user.IsActive,
                    user.IsEmailVerified,
                    passwordMatch,
                    UserFound: true));
            }

            var allMatch = results.All(r => r.UserFound && r.IsActive && r.IsEmailVerified && r.PasswordMatch);
            var anyMismatch = results.Any(r => r.UserFound && !r.PasswordMatch);
            return new VerifyPasswordRunResult(results, allMatch, anyMismatch);
        }
        finally
        {
            password = string.Empty;
        }
    }

    private static ServiceProvider CreateScope(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        return services.BuildServiceProvider();
    }
}

public sealed class VerifyPasswordOptions
{
    public required string ConnectionString { get; init; }

    public required string ManifestPath { get; init; }

    public int SampleSize { get; init; } = 3;
}
