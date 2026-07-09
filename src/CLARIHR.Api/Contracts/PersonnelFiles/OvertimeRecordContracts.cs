namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering an overtime record ("hora extra del empleado", REQ-007). <c>FactorApplied</c> is optional
/// — it defaults to the overtime type's reference factor when omitted (an override note is required only when it
/// differs). <c>DurationHours</c> + <c>DurationMinutes</c> (minutes 0–59, positive total) yield the derived decimal
/// hours. <c>AssignedPositionPublicId</c> is optional — the employee's principal plaza is resolved when omitted
/// (D-12). <c>RequesterFilePublicId</c> (the trío) is required on the HR channel; on the employee self-service
/// portal channel it is ignored (the requester is the subject employee). <c>PayrollPeriodLabel</c> is mandatory;
/// the period reference + end date are optional (degraded mode — no hard FK in PR-3).
/// </summary>
public sealed record AddOvertimeRecordRequest(
    DateOnly WorkDate,
    Guid OvertimeTypePublicId,
    decimal? FactorApplied,
    string? FactorOverrideNote,
    int DurationHours,
    int DurationMinutes,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid JustificationTypePublicId,
    string? Observations,
    Guid? AssignedPositionPublicId,
    Guid? RequesterFilePublicId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Body for editing an overtime record (EN_REVISION only). Same shape as the create body.</summary>
public sealed record UpdateOvertimeRecordRequest(
    DateOnly WorkDate,
    Guid OvertimeTypePublicId,
    decimal? FactorApplied,
    string? FactorOverrideNote,
    int DurationHours,
    int DurationMinutes,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid JustificationTypePublicId,
    string? Observations,
    Guid? AssignedPositionPublicId,
    Guid? RequesterFilePublicId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Body for annulling (retiro) an EN_REVISION overtime record (reason mandatory).</summary>
public sealed record AnnulOvertimeRecordRequest(string Reason);

/// <summary>Body for re-imputing ("enviar a otro periodo", RF-005) an AUTORIZADA record's payroll destination.</summary>
public sealed record RetargetOvertimeRecordPeriodRequest(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Body for the authorizer resolution: <c>TargetStatusCode</c> = AUTORIZADA (authorize) or RECHAZADA (reject — note mandatory).</summary>
public sealed record ResolveOvertimeRecordRequest(string TargetStatusCode, string? Note);

/// <summary>Body for the authorizer revocation of an AUTORIZADA record (reason mandatory).</summary>
public sealed record RevokeOvertimeRecordRequest(string Reason);
