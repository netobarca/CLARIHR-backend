namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>Registers a retirement request (RF-001). Captured by HR — no self-service in Fase 1 (D-03).</summary>
public sealed record AddRetirementRequestRequest(
    Guid RequesterFilePublicId,
    DateTime RequestDate,
    DateTime RetirementDate,
    string RetirementCategoryCode,
    string RetirementReasonCode,
    string? Notes);

/// <summary>Replaces the business fields of a SOLICITADA request (RF-003 / RN-003.1).</summary>
public sealed record UpdateRetirementRequestRequest(
    Guid RequesterFilePublicId,
    DateTime RequestDate,
    DateTime RetirementDate,
    string RetirementCategoryCode,
    string RetirementReasonCode,
    string? Notes);

/// <summary>Annuls an open request (RN-005). The note is optional.</summary>
public sealed record CancelRetirementRequestRequest(string? Notes);

/// <summary>
/// Authorizer resolution (RF-004): <c>AUTORIZADA</c> (note optional) or <c>RECHAZADA</c> (note mandatory,
/// RN-004.3).
/// </summary>
public sealed record ResolveRetirementRequestRequest(
    string TargetStatusCode,
    string? Notes);

/// <summary>
/// Executes an AUTORIZADA retirement (RF-006). Optionally marks the employee as not rehirable (D-18).
/// </summary>
public sealed record ExecuteRetirementRequestRequest(
    bool BlockRehire,
    string? RehireBlockReason);

/// <summary>Reverts an EJECUTADA retirement within the 30-day window (RF-010); the reason is mandatory.</summary>
public sealed record RevertRetirementRequestRequest(string Reason);
