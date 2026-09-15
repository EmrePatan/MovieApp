using System.Globalization;
using MovieApp.Application.Models.Credits;

namespace MovieApp.Application.Services.Credits;

public static class CreditsNormalizer
{
    public static CreditsResult Normalize(CreditsResult credits) =>
        new(NormalizeCast(credits.Cast), NormalizeCrew(credits.Crew));

    private static List<CastMemberResult> NormalizeCast(IReadOnlyList<CastMemberResult> cast)
    {
        var grouped = cast
            .GroupBy(member => GetPersonKey(member.ProviderPersonId, member.Name))
            .Select(group =>
            {
                var ordered = group.OrderBy(member => member.Order).ToList();
                var primary = ordered[0];
                var roles = MergeRoles(ordered);
                var character = primary.Character ?? (roles.Count > 0 ? roles[0].Character : null);

                return new CastMemberResult(
                    primary.ProviderPersonId,
                    primary.Name,
                    character,
                    primary.ProfileImagePath ?? ordered.Select(member => member.ProfileImagePath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)),
                    ordered.Min(member => member.Order),
                    ordered.Max(member => member.TotalEpisodeCount),
                    roles.Count > 0 ? roles : null);
            })
            .OrderBy(member => member.Order)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ToList();

        return grouped;
    }

    private static List<CrewMemberResult> NormalizeCrew(IReadOnlyList<CrewMemberResult> crew)
    {
        return crew
            .Where(member => !string.IsNullOrWhiteSpace(member.Name))
            .GroupBy(member => (GetPersonKey(member.ProviderPersonId, member.Name), NormalizeDepartment(member.Department)))
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(member => member.Name, StringComparer.Ordinal)
                    .ThenBy(member => member.Department, StringComparer.Ordinal)
                    .ToList();
                var primary = ordered[0];
                var jobs = ordered
                    .SelectMany(member => member.Jobs)
                    .Where(job => !string.IsNullOrWhiteSpace(job))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(job => job, StringComparer.Ordinal)
                    .ToList();

                return new CrewMemberResult(
                    primary.ProviderPersonId,
                    primary.Name,
                    primary.Department,
                    jobs,
                    primary.ProfileImagePath ?? ordered.Select(member => member.ProfileImagePath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)));
            })
            .OrderBy(member => member.Department, StringComparer.Ordinal)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<CastRoleResult> MergeRoles(IReadOnlyList<CastMemberResult> members)
    {
        return members
            .SelectMany(member => member.Roles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role.Character))
            .GroupBy(role => (role.Character!.Trim(), role.EpisodeCount))
            .Select(group => group.First())
            .OrderByDescending(role => role.EpisodeCount ?? 0)
            .ThenBy(role => role.Character, StringComparer.Ordinal)
            .ToList();
    }

    private static string GetPersonKey(int? providerPersonId, string name) =>
        providerPersonId?.ToString(CultureInfo.InvariantCulture) ?? $"name:{name.Trim()}";

    private static string? NormalizeDepartment(string? department) =>
        string.IsNullOrWhiteSpace(department) ? null : department.Trim();
}
