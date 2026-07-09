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
