namespace MovieApp.Application.Configuration;

public sealed class AvatarStorageOptions
{
    public const string SectionName = "AvatarStorage";

    public string Endpoint { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(BucketName) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(PublicBaseUrl);
}
