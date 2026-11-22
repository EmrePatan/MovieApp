using System.Net.Mail;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Validation;

public static class ProfileValidator
{
    public const int DisplayNameMaxLength = 100;

    public const int BioMaxLength = 300;

    public const int SocialHandleMaxLength = 64;

    public const int WebsiteMaxLength = 200;

    public const int EmailMaxLength = 320;

    public static SearchQueryValidationResult ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return SearchQueryValidationResult.Failure("Display name is required.");
        }

        if (displayName.Trim().Length > DisplayNameMaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Display name must not exceed {DisplayNameMaxLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateBio(string? bio)
    {
        if (string.IsNullOrWhiteSpace(bio))
        {
            return SearchQueryValidationResult.Success();
        }

        if (bio.Trim().Length > BioMaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Bio must not exceed {BioMaxLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateSocialLinks(ProfileSocialLinksResult? socialLinks)
    {
        if (socialLinks is null)
        {
            return SearchQueryValidationResult.Success();
        }

        foreach (var (name, value, maxLength, isWebsite) in new (string Name, string? Value, int Max, bool IsWebsite)[]
                 {
                     ("X", socialLinks.X, SocialHandleMaxLength, false),
                     ("Instagram", socialLinks.Instagram, SocialHandleMaxLength, false),
                     ("Snapchat", socialLinks.Snapchat, SocialHandleMaxLength, false),
                     ("TikTok", socialLinks.Tiktok, SocialHandleMaxLength, false),
                     ("YouTube", socialLinks.Youtube, SocialHandleMaxLength, false),
                     ("Twitch", socialLinks.Twitch, SocialHandleMaxLength, false),
                     ("Threads", socialLinks.Threads, SocialHandleMaxLength, false),
                     ("Website", socialLinks.Website, WebsiteMaxLength, true),
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var trimmed = value.Trim();
            if (trimmed.Length > maxLength)
            {
                return SearchQueryValidationResult.Failure(
                    $"{name} must not exceed {maxLength} characters.");
            }

            if (isWebsite && !IsValidHttpUrl(trimmed))
            {
                return SearchQueryValidationResult.Failure("Website must be a valid http(s) URL.");
            }
        }

        return SearchQueryValidationResult.Success();
    }

    private static bool IsValidHttpUrl(string value)
    {
        var candidate = value;
        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "https://" + candidate;
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static SearchQueryValidationResult ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return SearchQueryValidationResult.Failure("Email is required.");
        }

        var trimmedEmail = email.Trim();
        if (trimmedEmail.Length > EmailMaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Email must not exceed {EmailMaxLength} characters.");
        }

        if (!IsValidEmail(trimmedEmail))
        {
            return SearchQueryValidationResult.Failure("Email format is invalid.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateCurrentPassword(string? currentPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            return SearchQueryValidationResult.Failure("Current password is required.");
        }

        return SearchQueryValidationResult.Success();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
