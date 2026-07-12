namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering a one-time deduction ("descuento eventual" — a fine, a damaged asset, an advance…).
/// <c>AssignedPositionPublicId</c> is optional (the principal plaza is used when omitted; a deduction carries NO
/// cost center). The concept must be an ACTIVE, NON-STATUTORY <c>Egreso</c> concept — ISSS/AFP/Renta are payroll
/// law, not one-off charges, and are rejected.
/// <para><b>The amount belongs to the server.</b> With <c>IsFixedValue = true</c>, send <c>Amount</c> and no
/// components. Otherwise send a <c>CalculationMethod</c> — <c>CANTIDAD_POR_VALOR</c> (quantity × unit value ×
/// multiplier; the multiplier defaults to 1.00) or <c>PORCENTAJE_SOBRE_BASE</c> (percentage % of the base) — with
/// its components: the server DERIVES the amount from them. <c>Amount</c> may then be OMITTED; if you do send it
/// and it does not follow from the components, the request is rejected with `422`
/// <c>ONE_TIME_DEDUCTION_AMOUNT_MISMATCH</c> carrying the expected figure.</para>
/// <para><c>RequesterFilePublicId</c> is the trío's requester: it is snapshotted and, crucially, its linked login
/// cannot later decide the deduction (anti-self TRIPLE).</para>
/// </summary>
public sealed record AddOneTimeDeductionRequest(
    DateOnly DeductionDate,
    string? Reference,
    string ConceptTypeCode,
    string? Observations,
    bool IsFixedValue,
    string? CalculationMethod,
    decimal? Quantity,
    decimal? UnitValue,
    decimal? Multiplier,
    decimal? Percentage,
    decimal? BaseAmount,
    decimal? Amount,
    string? CurrencyCode,
    Guid? AssignedPositionPublicId,
    Guid RequesterFilePublicId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Body for editing a one-time deduction (EN_REVISION only). Same shape as the create body.</summary>
public sealed record UpdateOneTimeDeductionRequest(
    DateOnly DeductionDate,
    string? Reference,
    string ConceptTypeCode,
    string? Observations,
    bool IsFixedValue,
    string? CalculationMethod,
    decimal? Quantity,
    decimal? UnitValue,
    decimal? Multiplier,
    decimal? Percentage,
    decimal? BaseAmount,
    decimal? Amount,
    string? CurrencyCode,
    Guid? AssignedPositionPublicId,
    Guid RequesterFilePublicId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Body for re-targeting the payroll destination of an AUTORIZADO deduction ("enviar a otro periodo").</summary>
public sealed record RetargetOneTimeDeductionPeriodRequest(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Body for annulling an EN_REVISION deduction (reason mandatory).</summary>
public sealed record AnnulOneTimeDeductionRequest(string Reason);

/// <summary>Body for the authorizer resolution: <c>TargetStatusCode</c> = AUTORIZADO or RECHAZADO (note mandatory).</summary>
public sealed record ResolveOneTimeDeductionRequest(string TargetStatusCode, string? Note);

/// <summary>Body for the authorizer revocation of an AUTORIZADO deduction (reason mandatory).</summary>
public sealed record RevokeOneTimeDeductionRequest(string Reason);

/// <summary>
/// Body for charging a one-time deduction (its single application). <c>AppliedDate</c> defaults to today;
/// <c>PayrollPeriodPublicId</c> overrides the deduction's own target period (validated active).
/// </summary>
public sealed record ApplyOneTimeDeductionRequest(
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes);

/// <summary>Body for REVERTING the application (reason mandatory) — the deduction returns to AUTORIZADO.</summary>
public sealed record AnnulOneTimeDeductionApplicationRequest(string Reason);

/// <summary>
/// Body for the company-wide apply-period batch: charges every AUTORIZADO deduction of <c>PayrollTypeCode</c>
/// (optionally narrowed to a payroll period). <c>ExcludedDeductionPublicIds</c> postpones them. The batch is
/// ATOMIC: any conflict rolls the whole run back (422).
/// </summary>
public sealed record ApplyOneTimeDeductionPeriodRequest(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    IReadOnlyCollection<Guid>? ExcludedDeductionPublicIds);

/// <summary>Body for the company-wide pending work list (the AUTORIZADO deductions still to be charged).</summary>
public sealed record QueryOneTimeDeductionPendingRequest(
    string? PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    Guid? EmployeeId);

/// <summary>
/// Body for the company-wide one-time-deduction bandeja. Every filter is optional; when <c>StatusCode</c> is
/// omitted every status is listed. The StatusCounts and the per-currency totals always cover EVERY status.
/// </summary>
public sealed record QueryOneTimeDeductionsRequest(
    Guid? EmployeeId,
    string? StatusCode,
    string? ConceptTypeCode,
    string? PayrollTypeCode,
    DateOnly? DeductionFrom,
    DateOnly? DeductionTo,
    int? PageNumber,
    int? PageSize);
