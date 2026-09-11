namespace MovieApp.Domain.Reviews;

public static class ReviewContentRules
{
    public const int MaxLength = 5000;

    public static string Normalize(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var trimmed = content.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Review content must not exceed {MaxLength} characters.",
                nameof(content));
        }

        return trimmed;
    }
}
