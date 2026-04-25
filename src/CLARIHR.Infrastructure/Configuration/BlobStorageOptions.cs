namespace CLARIHR.Infrastructure.Configuration;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string? ConnectionString { get; init; }

    public string? AccountName { get; init; }

    public string ProfileImagesContainer { get; init; } = "clarihr-profile-images";

    public string PersonnelDocumentsContainer { get; init; } = "clarihr-personnel-documents";

    public string ReportExportsContainer { get; init; } = "clarihr-report-exports";

    public int ProfileImageSasTtlMinutes { get; init; } = 15;

    public int PersonnelDocumentSasTtlMinutes { get; init; } = 15;

    public int ReportExportSasTtlMinutes { get; init; } = 15;

    public int ReportExportRetentionHours { get; init; } = 24;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) &&
        !string.IsNullOrWhiteSpace(AccountName) &&
        !string.IsNullOrWhiteSpace(ProfileImagesContainer);

    public bool IsReportExportStorageConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) &&
        !string.IsNullOrWhiteSpace(AccountName) &&
        !string.IsNullOrWhiteSpace(ReportExportsContainer);

    public bool IsPersonnelDocumentStorageConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) &&
        !string.IsNullOrWhiteSpace(AccountName) &&
        !string.IsNullOrWhiteSpace(PersonnelDocumentsContainer);
}
