namespace CLARIHR.Domain.Files;

public enum FileStatus
{
    PendingUpload,
    Active,
    Failed,
    Deleted,
    Quarantined
}

public enum FilePurpose
{
    ProfileImage,
    PersonnelDocument,
    ReportExport,
    CompanyLogo,
    Attachment,
    MedicalClaimDocument,
    OffPayrollTransactionDocument,
    EconomicAidRequestDocument,
    CertificateRequestDocument,
    IncapacityDocument,
    CompensatoryTimeDocument,
    RecognitionDocument,
    DisciplinaryActionDocument
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
