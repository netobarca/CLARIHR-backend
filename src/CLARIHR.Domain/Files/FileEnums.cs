namespace CLARIHR.Domain.Files;

public enum FileStatus
{
    PendingUpload,
    Active,
    Failed,
    Deleted,
    Quarantined
}

public enum FileVisibility
{
    Private,
    Authenticated,
    Public
}

public enum FilePurpose
{
    ProfileImage,
    PersonnelDocument,
    ReportExport,
    CompanyLogo,
    Attachment
}

public enum StorageProvider
{
    AzureBlob
}

public enum FileUploadType
{
    DirectUpload,
    ServerSideUpload
}
