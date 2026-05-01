using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Files.Common;

public static class FileErrors
{
    public static readonly Error FileNotFound = new(
        "files.not_found",
        "The requested file was not found.",
        ErrorType.NotFound);

    public static readonly Error FileNotActive = new(
        "files.not_active",
        "The file is not in an active state.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FileNotPendingUpload = new(
        "files.not_pending_upload",
        "The file is not pending upload.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FileOwnershipMismatch = new(
        "files.ownership_mismatch",
        "You do not own this file.",
        ErrorType.Forbidden);

    public static readonly Error FileTenantMismatch = new(
        "files.tenant_mismatch",
        "The file does not belong to your organization.",
        ErrorType.Forbidden);

    public static readonly Error StorageProviderNotConfigured = new(
        "files.provider_not_configured",
        "The storage provider is not configured.",
        ErrorType.ServiceUnavailable);

    public static readonly Error PurposeNotConfigured = new(
        "files.purpose_not_configured",
        "No rules are configured for the specified file purpose.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FileTooLarge = new(
        "files.too_large",
        "The file exceeds the maximum allowed size.",
        ErrorType.PayloadTooLarge);

    public static Error ContentTypeNotAllowed(string contentType) => new(
        "files.content_type_not_allowed",
        $"The content type '{contentType}' is not allowed for this purpose.",
        ErrorType.UnprocessableEntity);

    public static Error ExtensionNotAllowed(string extension) => new(
        "files.extension_not_allowed",
        $"The file extension '{extension}' is not allowed for this purpose.",
        ErrorType.UnprocessableEntity);

    public static readonly Error UploadNotFoundInStorage = new(
        "files.upload_not_found",
        "The uploaded file was not found in storage. The upload may have failed or the session expired.",
        ErrorType.UnprocessableEntity);

    public static Error InvalidPurpose(string purpose) => new(
        "files.invalid_purpose",
        $"'{purpose}' is not a valid file purpose.",
        ErrorType.Validation);
}
