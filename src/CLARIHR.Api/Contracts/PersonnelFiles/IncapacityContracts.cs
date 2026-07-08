namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering an incapacity ("incapacidad"). Master references travel as public ids. A null
/// <c>endDate</c> registers an open-ended record (only when the risk allows it — D-11). The constancia
/// (D-22) is referenced by <c>documentFilePublicId</c> (an already-uploaded file with
/// <c>purpose = IncapacityDocument</c>), mandatory when the company preference requires it.
/// </summary>
public sealed record AddIncapacityRequest(
    Guid RiskPublicId,
    Guid IncapacityTypePublicId,
    Guid? MedicalClinicPublicId,
    Guid? AssignedPositionPublicId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodDefinitionPublicId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Notes,
    Guid? DocumentFilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentObservations);

/// <summary>Body for editing an incapacity's business fields (HR); the breakdown is recalculated server-side.</summary>
public sealed record UpdateIncapacityRequest(
    Guid RiskPublicId,
    Guid IncapacityTypePublicId,
    Guid? MedicalClinicPublicId,
    Guid? AssignedPositionPublicId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodDefinitionPublicId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Notes,
    Guid? DocumentFilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentObservations);

/// <summary>Body for closing an open-ended incapacity by fixing its end date (D-11).</summary>
public sealed record CloseIncapacityRequest(DateOnly EndDate);

/// <summary>Body for annulling an incapacity; the reason is mandatory.</summary>
public sealed record AnnulIncapacityRequest(string Reason);

/// <summary>
/// Body for registering an extension ("prórroga") of an incapacity. The start date is derived (source end
/// date + 1, RN-04), so it is not provided here.
/// </summary>
public sealed record AddIncapacityExtensionRequest(
    Guid RiskPublicId,
    Guid IncapacityTypePublicId,
    Guid? MedicalClinicPublicId,
    Guid? AssignedPositionPublicId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodDefinitionPublicId,
    DateOnly EndDate,
    string? Notes,
    Guid? DocumentFilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentObservations);

/// <summary>
/// Body for attaching a supporting document to an incapacity. <c>FilePublicId</c> references an already-uploaded
/// file (purpose <c>IncapacityDocument</c>); <c>DocumentTypeCatalogItemPublicId</c> is an optional classification.
/// </summary>
public sealed record AddIncapacityDocumentRequest(
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations);
