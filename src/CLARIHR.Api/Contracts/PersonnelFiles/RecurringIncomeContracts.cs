namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for registering a recurring income ("ingreso cíclico"). <c>AssignedPositionPublicId</c> is optional — when
/// omitted the employee's principal plaza is resolved and the cost center is derived from it (P-15). For an
/// indefinite plan (<c>IsIndefinite</c> = true) both <c>InstallmentCount</c> and <c>TotalAmount</c> must be null;
/// for a finite plan at least one of them is supplied and the missing one is derived. <c>SettlementActionCode</c>
/// is <c>PAGAR_SALDO</c> or <c>CANCELAR</c> (PAGAR_SALDO is invalid for an indefinite plan).
/// </summary>
public sealed record AddRecurringIncomeRequest(
    DateOnly RegistrationDate,
    string? Reference,
    string RecurringIncomeTypeCode,
    string ConceptTypeCode,
    string? Observations,
    Guid? AssignedPositionPublicId,
    DateOnly InstallmentStartDate,
    string CurrencyCode,
    string PayrollTypeCode,
    string InstallmentFrequencyCode,
    bool IsIndefinite,
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    string SettlementActionCode);

/// <summary>Body for editing a recurring income's header + plan (EN_REVISION only). Same shape as the create body.</summary>
public sealed record UpdateRecurringIncomeRequest(
    DateOnly RegistrationDate,
    string? Reference,
    string RecurringIncomeTypeCode,
    string ConceptTypeCode,
    string? Observations,
    Guid? AssignedPositionPublicId,
    DateOnly InstallmentStartDate,
    string CurrencyCode,
    string PayrollTypeCode,
    string InstallmentFrequencyCode,
    bool IsIndefinite,
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    string SettlementActionCode);

/// <summary>Body for suspending (<c>Suspend</c> = true, note optional) or resuming (<c>Suspend</c> = false) an income.</summary>
public sealed record SetRecurringIncomeSuspensionRequest(bool Suspend, string? Note);

/// <summary>Body for closing an indefinite VIGENTE income by hand (reason mandatory — P-06).</summary>
public sealed record CloseRecurringIncomeRequest(string Reason);

/// <summary>Body for annulling an EN_REVISION income (reason mandatory).</summary>
public sealed record AnnulRecurringIncomeRequest(string Reason);

/// <summary>Body for the authorizer resolution: <c>TargetStatusCode</c> = VIGENTE (authorize) or RECHAZADO (reject — note mandatory).</summary>
public sealed record ResolveRecurringIncomeRequest(string TargetStatusCode, string? Note);

/// <summary>Body for the authorizer revocation of a VIGENTE income (reason mandatory).</summary>
public sealed record RevokeRecurringIncomeRequest(string Reason);
