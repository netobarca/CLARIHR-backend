namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering a recognition ("reconocimiento", REQ-003 D-02/D-07). The recognition type travels as a
/// public id and is resolved to its internal id + name snapshot by the handler (422
/// <c>RECOGNITION_TYPE_INVALID</c> when inactive/foreign). <c>EventDate</c> must be ≤ today. The
/// <c>Amount</c>/<c>CurrencyCode</c> pair is informational (RN-17): when an amount travels it must be positive
/// and carry its currency. The record starts EN_REVISION.
/// </summary>
public sealed record AddRecognitionRequest(
    Guid RecognitionTypePublicId,
    DateOnly EventDate,
    string Detail,
    decimal? Amount,
    string? CurrencyCode,
    Guid? AssignedPositionPublicId,
    string? Notes);

/// <summary>Body for editing a recognition's declarative fields while EN_REVISION (RN-01, manager-only).</summary>
public sealed record UpdateRecognitionRequest(
    Guid RecognitionTypePublicId,
    DateOnly EventDate,
    string Detail,
    decimal? Amount,
    string? CurrencyCode,
    Guid? AssignedPositionPublicId,
    string? Notes);

/// <summary>
/// Body for the single decision on a recognition (RN-01/RN-02). <c>Decision</c> is <c>APLICAR</c> or
/// <c>RECHAZAR</c>; the <c>Note</c> is mandatory when rejecting (RN-07). Requires the dedicated
/// <c>AuthorizeRecognitions</c> grant and enforces the double anti-self-approval check.
/// </summary>
public sealed record RecognitionDecisionRequest(string Decision, string? Note);

/// <summary>Body for annulling / revoking a recognition; the reason is mandatory (RN-07).</summary>
public sealed record RecognitionAnnulmentRequest(string Reason);

/// <summary>
/// Body for attaching a supporting document (diploma / memo) to a recognition. <c>FilePublicId</c> references an
/// already-uploaded file (purpose <c>RecognitionDocument</c>); <c>DocumentTypeCatalogItemPublicId</c> is an
/// optional classification.
/// </summary>
public sealed record AddRecognitionDocumentRequest(
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations);
