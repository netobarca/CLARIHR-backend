namespace CLARIHR.Infrastructure.Configuration;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string? ConnectionString { get; init; }

    public string? AccountName { get; init; }

    public string ProfileImagesContainer { get; init; } = "clarihr-profile-images";

    public int ProfileImageSasTtlMinutes { get; init; } = 15;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) &&
        !string.IsNullOrWhiteSpace(AccountName) &&
        !string.IsNullOrWhiteSpace(ProfileImagesContainer);
}
