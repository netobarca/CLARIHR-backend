namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering a compensatory-time credit ("acreditación de tiempo compensatorio"). The type travels as
/// a public id. The credited hours default to <c>hoursWorked × factor</c>; supply <c>hoursCreditedOverride</c> +
/// <c>overrideNote</c> to record a manual adjustment (RN-02). The authorization document (D-20) is referenced by
/// <c>authorizationFilePublicId</c> (an already-uploaded file with <c>purpose = CompensatoryTimeDocument</c>),
/// mandatory when the company preference requires it, and attached in the same transaction.
/// </summary>
public sealed record AddCompensatoryTimeCreditRequest(
    Guid CompensatoryTimeTypePublicId,
    DateOnly WorkDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal HoursWorked,
    decimal? HoursCreditedOverride,
    string? OverrideNote,
    string WorkDetail,
    string AuthorizedByText,
    Guid? AssignedPositionPublicId,
    Guid? OvertimeRecordPublicId,
    string? Notes,
    Guid? AuthorizationFilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentObservations);

/// <summary>Body for editing a compensatory-time credit's business fields (HR); attachments are managed via the document sub-resource.</summary>
public sealed record UpdateCompensatoryTimeCreditRequest(
    Guid CompensatoryTimeTypePublicId,
    DateOnly WorkDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal HoursWorked,
    decimal? HoursCreditedOverride,
    string? OverrideNote,
    string WorkDetail,
    string AuthorizedByText,
    Guid? AssignedPositionPublicId,
    Guid? OvertimeRecordPublicId,
    string? Notes);

/// <summary>Body for annulling a compensatory-time credit; the reason is mandatory.</summary>
public sealed record AnnulCompensatoryTimeCreditRequest(string Reason);

/// <summary>
/// Body for attaching an authorization document to a compensatory-time credit. <c>FilePublicId</c> references an
/// already-uploaded file (purpose <c>CompensatoryTimeDocument</c>); <c>DocumentTypeCatalogItemPublicId</c> is an
/// optional classification.
/// </summary>
public sealed record AddCompensatoryTimeCreditDocumentRequest(
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations);
