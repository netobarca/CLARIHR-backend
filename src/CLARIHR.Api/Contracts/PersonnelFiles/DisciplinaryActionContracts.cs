namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering a disciplinary action ("amonestación", REQ-003 D-02/D-08). The type and cause travel as
/// public ids and are resolved to their internal ids + name snapshots by the handler (422
/// <c>DISCIPLINARY_ACTION_TYPE_INVALID</c> / <c>DISCIPLINARY_ACTION_CAUSE_INVALID</c> when inactive/foreign).
/// <c>IncidentDate</c> must be ≤ today. The suspension block
/// (<c>SuspensionStartDate</c>/<c>SuspensionEndDate</c>) is only allowed on a type that applies suspension
/// (RN-05); the deduction block (<c>HasPayrollDeduction</c> + <c>DeductionAmount</c>) requires a positive amount
/// (RN-06). The optional <c>DeductionConceptTypeCode</c> is an editable egreso reference (default from the
/// cause); the authoritative concept is frozen at Apply (aclaración №5). The record starts EN_REVISION.
/// </summary>
public sealed record AddDisciplinaryActionRequest(
    Guid DisciplinaryActionTypePublicId,
    Guid DisciplinaryActionCausePublicId,
    DateOnly IncidentDate,
    string FactsDetail,
    bool HasPayrollDeduction,
    decimal? DeductionAmount,
    string? CurrencyCode,
    string? DeductionConceptTypeCode,
    DateOnly? SuspensionStartDate,
    DateOnly? SuspensionEndDate,
    Guid? AssignedPositionPublicId,
    string? Notes);

/// <summary>Body for editing a disciplinary action's declarative fields while EN_REVISION (RN-01, manager-only).</summary>
public sealed record UpdateDisciplinaryActionRequest(
    Guid DisciplinaryActionTypePublicId,
    Guid DisciplinaryActionCausePublicId,
    DateOnly IncidentDate,
    string FactsDetail,
    bool HasPayrollDeduction,
    decimal? DeductionAmount,
    string? CurrencyCode,
    string? DeductionConceptTypeCode,
    DateOnly? SuspensionStartDate,
    DateOnly? SuspensionEndDate,
    Guid? AssignedPositionPublicId,
    string? Notes);

/// <summary>
/// Body for the single decision on a disciplinary action (RN-01/RN-02). <c>Decision</c> is <c>APLICAR</c> or
/// <c>RECHAZAR</c>; the <c>Note</c> is mandatory when rejecting (RN-07). Requires the dedicated
/// <c>AuthorizeDisciplinaryActions</c> grant and enforces the double anti-self-approval check.
/// </summary>
public sealed record DisciplinaryActionDecisionRequest(string Decision, string? Note);

/// <summary>Body for annulling / revoking a disciplinary action; the reason is mandatory (RN-07).</summary>
public sealed record DisciplinaryActionAnnulmentRequest(string Reason);

/// <summary>
/// Body for attaching a supporting document (acta / descargo) to a disciplinary action. <c>FilePublicId</c>
/// references an already-uploaded file (purpose <c>DisciplinaryActionDocument</c>);
/// <c>DocumentTypeCatalogItemPublicId</c> is an optional classification.
/// </summary>
public sealed record AddDisciplinaryActionDocumentRequest(
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations);
