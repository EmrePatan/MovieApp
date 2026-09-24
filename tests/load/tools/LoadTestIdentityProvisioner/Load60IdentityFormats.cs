using System.Text.RegularExpressions;
using MovieApp.Application.Validation;
using MovieApp.Domain.Users;

namespace MovieApp.LoadTestIdentityProvisioner;

public static partial class Load60IdentityFormats
{
    public const string DefaultEmailDomain = "loadtest.invalid";

    public const string HarnessIdPrefix = "load60-";

    public const string DisplayNamePrefix = "LOAD60 #";

    private static readonly Regex LocalPartPattern = Load60LocalPartRegex();

    public static bool IsReservedDomainSupported(string domain = DefaultEmailDomain) =>
        ProfileValidator.ValidateEmail($"probe@{domain}").IsValid &&
        ProfileValidator.ValidateEmail($"{HarnessIdPrefix}001@{domain}").IsValid;

    public static IReadOnlyList<Load60IdentitySlot> BuildSlots(int count, string emailDomain)
    {
        if (count < 1 || count > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 999.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(emailDomain);

        var domain = emailDomain.Trim().ToLowerInvariant();
        if (!IsReservedDomainSupported(domain))
        {
            throw new ArgumentException(
                $"Email domain '{domain}' is not accepted by current email validation. Supply a different --email-domain.",
                nameof(emailDomain));
        }

        var slots = new List<Load60IdentitySlot>(count);
        for (var index = 1; index <= count; index++)
        {
            var suffix = index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            var harnessId = $"{HarnessIdPrefix}{suffix}";
            var email = $"{harnessId}@{domain}";
            var validation = ProfileValidator.ValidateEmail(email);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Generated email failed validation: {validation.ErrorMessage}");
            }

            slots.Add(new Load60IdentitySlot(
                harnessId,
                email,
                UserEmailNormalizer.Normalize(email),
                $"{DisplayNamePrefix}{suffix}"));
        }

        return slots;
    }

    public static bool IsDedicatedLoad60NormalizedEmail(string normalizedEmail, string emailDomain)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return false;
        }

        var at = normalizedEmail.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at >= normalizedEmail.Length - 1)
        {
            return false;
        }

        var local = normalizedEmail[..at];
        var domain = normalizedEmail[(at + 1)..];
        if (!string.Equals(domain, emailDomain.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return false;
        }

        return LocalPartPattern.IsMatch(local);
    }

    public static bool IsDedicatedLoad60HarnessId(string harnessId) =>
        !string.IsNullOrWhiteSpace(harnessId) &&
        harnessId.StartsWith(HarnessIdPrefix, StringComparison.Ordinal) &&
        LocalPartPattern.IsMatch(harnessId);

    [GeneratedRegex(@"^load60-\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex Load60LocalPartRegex();
}

public sealed record Load60IdentitySlot(
    string HarnessId,
    string Email,
    string NormalizedEmail,
    string DisplayName);
