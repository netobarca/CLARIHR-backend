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

/// <summary>
/// Body for registering the single application of an AUTORIZADA overtime record (RF-011). The hours do NOT travel
/// (they are the record's own duration/factor). <c>AppliedDate</c> defaults to today when omitted;
/// <c>PayrollPeriodPublicId</c> (optional) imputes the application to a company payroll-period instance (validated
/// active, FK real) — when omitted the application inherits the record's declared destination. The record's
/// <c>concurrencyToken</c> travels in the <c>If-Match</c> header.
/// </summary>
public sealed record ApplyOvertimeRecordApplicationRequest(
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes);

/// <summary>Body for annulling (reverting) the active application of an overtime record (RF-013); the reason is mandatory.</summary>
public sealed record AnnulOvertimeRecordApplicationRequest(string Reason);

/// <summary>
/// Body for the company-wide apply-period batch (RF-012): applies every AUTORIZADA overtime record of
/// <c>PayrollTypeCode</c> whose work date has elapsed — including the "atrasados". Provide a
/// <c>PayrollPeriodPublicId</c> (FK real; its id + label are snapshotted onto the applications) or a bare
/// <c>PayrollPeriodLabel</c> to override the destination for every applied record; omit both to default each
/// application to its record's declared destination. <c>ExcludedRecordPublicIds</c> postpones records.
/// </summary>
public sealed record ApplyOvertimePeriodRequest(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string? PayrollPeriodLabel,
    IReadOnlyCollection<Guid>? ExcludedRecordPublicIds);

/// <summary>Body for the pending/overdue tray (RF-012): the AUTORIZADA overtime records without an active
/// application, optionally filtered by payroll type and/or only the overdue ones.</summary>
public sealed record QueryOvertimeRecordPendingRequest(
    string? PayrollTypeCode,
    bool? OnlyOverdue);

/// <summary>
/// Body for the company-wide overtime advanced search (RF-011 / §0.16). Every filter is optional; when
/// <c>StatusCodes</c> is empty every status is listed (the StatusCounts always span every status). The response
/// carries the paginated items, the per-status counts, the global total HOURS and the totals-by-type buckets
/// (<c>{overtimeTypeCode, overtimeTypeName, count, totalHours}</c>). Totals are EN HORAS — the module carries no
/// money; there is NO dimensional groupBy. <c>OriginChannel</c> filters by the dual channel (<c>RRHH</c>/<c>PORTAL</c>).
/// </summary>
public sealed record QueryOvertimeRecordsRequest(
    IReadOnlyCollection<string>? StatusCodes,
    Guid? EmployeeId,
    Guid? OvertimeTypePublicId,
    Guid? JustificationTypePublicId,
    DateOnly? FromWorkDate,
    DateOnly? ToWorkDate,
    string? PayrollTypeCode,
    string? PayrollPeriod,
    Guid? RequesterFilePublicId,
    string? OriginChannel,
    Guid? AssignedPositionPublicId,
    string? Search,
    int? PageNumber,
    int? PageSize);
