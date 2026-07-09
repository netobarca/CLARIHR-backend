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

/// <summary>
/// Body for applying the NEXT installment of a VIGENTE recurring income (RF-006). The installment number and
/// amount are derived by the rules (not editable, P-04). <c>AppliedDate</c> defaults to today when omitted;
/// <c>PayrollPeriodPublicId</c> (optional) imputes the installment to a company payroll-period instance (validated
/// active). The income's <c>concurrencyToken</c> travels in the <c>If-Match</c> header.
/// </summary>
public sealed record ApplyRecurringIncomeInstallmentRequest(
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes);

/// <summary>Body for annulling an applied installment (RF-008); the reason is mandatory.</summary>
public sealed record AnnulRecurringIncomeInstallmentRequest(string Reason);

/// <summary>
/// Body for the company-wide apply-period batch (RF-007): applies every due installment of the VIGENTE incomes of
/// <c>PayrollTypeCode</c> up to the cutoff. Provide a <c>PayrollPeriodPublicId</c> (its end date is the cutoff and
/// its id/label are snapshotted) or a bare <c>CutoffDate</c>. <c>ExcludedIncomePublicIds</c> postpones incomes.
/// </summary>
public sealed record ApplyRecurringIncomePeriodRequest(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    IReadOnlyCollection<Guid>? ExcludedIncomePublicIds);

/// <summary>
/// Body for the company-wide recurring-income bandeja query (RF-010). Every filter is optional; when
/// <c>StatusCode</c> is omitted every status is listed (annulled / rejected included). The StatusCounts are always
/// computed over every status.
/// </summary>
public sealed record QueryRecurringIncomesRequest(
    Guid? EmployeeId,
    string? StatusCode,
    string? RecurringIncomeTypeCode,
    string? PayrollTypeCode,
    DateTime? RegisteredFromUtc,
    DateTime? RegisteredToUtc,
    int? PageNumber,
    int? PageSize);

/// <summary>
/// Body for the company-wide pending-installments bandeja query (RF-011). The cutoff is the
/// <c>PayrollPeriodPublicId</c> end date, the bare <c>CutoffDate</c>, or today; <c>StartDate</c> narrows the lower
/// bound; <c>PayrollTypeCode</c> / <c>EmployeeId</c> scope the VIGENTE-income scan.
/// </summary>
public sealed record QueryPendingRecurringIncomeInstallmentsRequest(
    string? PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    DateOnly? StartDate,
    Guid? EmployeeId,
    int? PageNumber,
    int? PageSize);
