namespace CLARIHR.Infrastructure.Files.Configuration;

public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    public string DefaultProvider { get; init; } = "AzureBlob";

    public AzureBlobProviderOptions AzureBlob { get; init; } = new();

    public IReadOnlyDictionary<string, FilePurposeOptions> Purposes { get; init; } =
        new Dictionary<string, FilePurposeOptions>(StringComparer.OrdinalIgnoreCase);

    public FileCleanupOptions Cleanup { get; init; } = new();
}

public sealed class AzureBlobProviderOptions
{
    public string AccountName { get; init; } = string.Empty;

    /// <summary>
    /// Shared Key for the storage account. When set, the provider authenticates with
    /// a <c>StorageSharedKeyCredential</c> and signs SAS with the account key instead
    /// of a user-delegation key — the path that lets local dev run against the Azurite
    /// emulator (technical-debt doc 01 §3.5). Leave empty in production, which uses
    /// managed identity / <c>DefaultAzureCredential</c> + user-delegation SAS.
    /// </summary>
    public string AccountKey { get; init; } = string.Empty;

    public string BlobEndpoint { get; init; } = string.Empty;

    public string DefaultContainer { get; init; } = string.Empty;

    public int UploadUrlExpirationMinutes { get; init; } = 10;

    public int ReadUrlExpirationMinutes { get; init; } = 15;

    public bool UseManagedIdentity { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountName) &&
        !string.IsNullOrWhiteSpace(BlobEndpoint) &&
        !string.IsNullOrWhiteSpace(DefaultContainer);
}

public sealed class FilePurposeOptions
{
    public long MaxSizeBytes { get; init; }

    public IReadOnlyCollection<string> AllowedContentTypes { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> AllowedExtensions { get; init; } = Array.Empty<string>();

    public string DefaultProvider { get; init; } = "AzureBlob";

    public bool RequiresMalwareScan { get; init; }

    public string? ContainerOverride { get; init; }
}

public sealed class FileCleanupOptions
{
    public int IntervalMinutes { get; init; } = 30;

    public int BatchSize { get; init; } = 100;

    public int RetentionHours { get; init; } = 24;
}
