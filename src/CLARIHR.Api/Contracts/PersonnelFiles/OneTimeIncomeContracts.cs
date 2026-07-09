namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering a one-time income ("ingreso eventual", REQ-006). The value is either fixed
/// (<c>IsFixedValue</c> = true, <c>Amount</c> &gt; 0, no method or components) or computed (a
/// <c>CalculationMethod</c> — <c>CANTIDAD_POR_VALOR</c> or <c>PORCENTAJE_SOBRE_BASE</c> — with the matching
/// components; the server resolves the amount and, when <c>Amount</c> is supplied, cross-checks it after
/// rounding). <c>AssignedPositionPublicId</c> is optional — when omitted the employee's principal plaza is
/// resolved and the cost center is derived from it (P-15). <c>CurrencyCode</c> is optional — it defaults to the
/// company preference currency. <c>RequesterFilePublicId</c> (the trío) is mandatory. <c>PayrollPeriodLabel</c>
/// is mandatory; the period reference + end date are optional (the "atrasado" mark needs the end date).
/// </summary>
public sealed record AddOneTimeIncomeRequest(
    DateOnly IncomeDate,
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

/// <summary>Body for editing a one-time income (EN_REVISION only). Same shape as the create body.</summary>
public sealed record UpdateOneTimeIncomeRequest(
    DateOnly IncomeDate,
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

/// <summary>Body for annulling (retiro) an EN_REVISION income (reason mandatory).</summary>
public sealed record AnnulOneTimeIncomeRequest(string Reason);

/// <summary>Body for re-imputing ("enviar a otro periodo", RF-005) an AUTORIZADO income's payroll destination.</summary>
public sealed record RetargetOneTimeIncomePeriodRequest(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Body for the authorizer resolution: <c>TargetStatusCode</c> = AUTORIZADO (authorize) or RECHAZADO (reject — note mandatory).</summary>
public sealed record ResolveOneTimeIncomeRequest(string TargetStatusCode, string? Note);

/// <summary>Body for the authorizer revocation of an AUTORIZADO income (reason mandatory).</summary>
public sealed record RevokeOneTimeIncomeRequest(string Reason);

/// <summary>
/// Body for registering the single application of an AUTORIZADO one-time income (RF-011). The amount does NOT
/// travel (it is the income's own amount). <c>AppliedDate</c> defaults to today when omitted;
/// <c>PayrollPeriodPublicId</c> (optional) imputes the application to a company payroll-period instance (validated
/// active, FK real) — when omitted the application inherits the income's declared destination. The income's
/// <c>concurrencyToken</c> travels in the <c>If-Match</c> header.
/// </summary>
public sealed record ApplyOneTimeIncomeApplicationRequest(
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes);

/// <summary>Body for annulling (reverting) the active application of a one-time income (RF-013); the reason is mandatory.</summary>
public sealed record AnnulOneTimeIncomeApplicationRequest(string Reason);

/// <summary>
/// Body for the company-wide apply-period batch (RF-012): applies every AUTORIZADO one-time income of
/// <c>PayrollTypeCode</c> — including the "atrasados". Provide a <c>PayrollPeriodPublicId</c> (FK real; its id +
/// label are snapshotted onto the applications) or a bare <c>PayrollPeriodLabel</c> to override the destination for
/// every applied income; omit both to default each application to its income's declared destination.
/// <c>ExcludedIncomePublicIds</c> postpones incomes.
/// </summary>
public sealed record ApplyOneTimeIncomePeriodRequest(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string? PayrollPeriodLabel,
    IReadOnlyCollection<Guid>? ExcludedIncomePublicIds);

/// <summary>Body for the pending/overdue tray (RF-012): the AUTORIZADO one-time incomes without an active
/// application, optionally filtered by payroll type and/or only the overdue ones.</summary>
public sealed record QueryOneTimeIncomePendingRequest(
    string? PayrollTypeCode,
    bool? OnlyOverdue);

/// <summary>
/// Body for the company-wide one-time-income advanced search + aggregation (RF-008 / №14). Every filter is optional;
/// when <c>StatusCodes</c> is empty every status is listed (the StatusCounts always span every status). When
/// <c>GroupBy</c> is present the response also carries the aggregation buckets (composite key (dimension, currency);
/// an invalid dimension → 400). The allowed dimensions are <c>estado</c>, <c>tipo</c>, <c>empleado</c>,
/// <c>tipoPlanilla</c>, <c>periodo</c>, <c>centroCosto</c>, <c>moneda</c>, <c>mes</c>.
/// </summary>
public sealed record QueryOneTimeIncomesRequest(
    IReadOnlyCollection<string>? StatusCodes,
    Guid? EmployeeId,
    string? ConceptTypeCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool? IsFixedValue,
    string? PayrollTypeCode,
    string? PayrollPeriod,
    Guid? CostCenterPublicId,
    string? CurrencyCode,
    Guid? RequesterFilePublicId,
    string? Search,
    string? GroupBy,
    int? PageNumber,
    int? PageSize);
