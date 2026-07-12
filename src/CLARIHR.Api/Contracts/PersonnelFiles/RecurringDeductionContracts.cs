namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>One segment of the installment plan: installments <c>FromInstallment</c>..<c>ToInstallment</c> are
/// worth <c>InstallmentValue</c> each. The segments must be contiguous from 1 with no gaps or overlaps.
/// <c>ToInstallment</c> is null ONLY on the single open segment of an indefinite plan.</summary>
public sealed record RecurringDeductionSegmentRequest(
    int FromInstallment,
    int? ToInstallment,
    decimal InstallmentValue);

/// <summary>
/// Body for registering a recurring deduction ("descuento cíclico" — a credit discounted in installments).
/// <c>AssignedPositionPublicId</c> is optional — when omitted the employee's principal plaza is resolved (there is
/// no cost center on a deduction). <c>EffectiveDate</c> MAY be in the future: the credit is registered and
/// authorized, but no installment can be charged until that date is reached.
/// <para>The plan is expressed in exactly ONE of two ways: WITHOUT compound interest send <c>Segments</c> (and no
/// principal/rate/count); WITH <c>UsesCompoundInterest</c> send <c>PrincipalAmount</c> + <c>InterestRatePercent</c>
/// (NOMINAL ANNUAL) + <c>PlannedInstallments</c> and NO segments — the French-system amortization derives the
/// quota and the capital/interest split. A compound-interest credit cannot be indefinite.</para>
/// <para><c>ApplicationFrequencyCode</c> may be FASTER than <c>InstallmentFrequencyCode</c> (a monthly quota
/// applied fortnightly splits in 2); the inverse is rejected. <c>ExceptionMonths</c> (1..12) are skipped and push
/// the plan forward. <c>SettlementActionCode</c> is <c>DESCONTAR_SALDO</c> or <c>CANCELAR</c> (DESCONTAR_SALDO is
/// invalid for an indefinite plan). The financial institution is MANDATORY for external deduction concepts.</para>
/// </summary>
public sealed record AddRecurringDeductionRequest(
    DateOnly EffectiveDate,
    string Reference,
    string RecurringDeductionTypeCode,
    string ConceptTypeCode,
    string? FinancialInstitution,
    string? Observations,
    Guid? AssignedPositionPublicId,
    DateOnly InstallmentStartDate,
    IReadOnlyCollection<int>? ExceptionMonths,
    string CurrencyCode,
    string PayrollTypeCode,
    string InstallmentFrequencyCode,
    string ApplicationFrequencyCode,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    decimal? PrincipalAmount,
    decimal? InterestRatePercent,
    int? PlannedInstallments,
    IReadOnlyCollection<RecurringDeductionSegmentRequest>? Segments,
    string SettlementActionCode);

/// <summary>Body for editing a recurring deduction's header + plan (EN_REVISION only; the segments are
/// replace-all). Same shape as the create body.</summary>
public sealed record UpdateRecurringDeductionRequest(
    DateOnly EffectiveDate,
    string Reference,
    string RecurringDeductionTypeCode,
    string ConceptTypeCode,
    string? FinancialInstitution,
    string? Observations,
    Guid? AssignedPositionPublicId,
    DateOnly InstallmentStartDate,
    IReadOnlyCollection<int>? ExceptionMonths,
    string CurrencyCode,
    string PayrollTypeCode,
    string InstallmentFrequencyCode,
    string ApplicationFrequencyCode,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    decimal? PrincipalAmount,
    decimal? InterestRatePercent,
    int? PlannedInstallments,
    IReadOnlyCollection<RecurringDeductionSegmentRequest>? Segments,
    string SettlementActionCode);

/// <summary>Body for suspending (<c>Suspend</c> = true, note optional) or resuming (false) a credit.</summary>
public sealed record SetRecurringDeductionSuspensionRequest(bool Suspend, string? Note);

/// <summary>Body for closing an indefinite VIGENTE credit by hand (reason mandatory).</summary>
public sealed record CloseRecurringDeductionRequest(string Reason);

/// <summary>Body for annulling an EN_REVISION credit (reason mandatory).</summary>
public sealed record AnnulRecurringDeductionRequest(string Reason);

/// <summary>Body for the authorizer resolution: <c>TargetStatusCode</c> = VIGENTE (authorize) or RECHAZADO (reject — note mandatory).</summary>
public sealed record ResolveRecurringDeductionRequest(string TargetStatusCode, string? Note);

/// <summary>Body for the authorizer revocation of a VIGENTE credit (reason mandatory).</summary>
public sealed record RevokeRecurringDeductionRequest(string Reason);

/// <summary>
/// Body for applying the NEXT charge of a VIGENTE credit (RF-006). The charge number, its amount and its
/// capital/interest split are derived by the rules and are NOT editable. <c>AppliedDate</c> defaults to today when
/// omitted; <c>PayrollPeriodPublicId</c> (optional) imputes the charge to a company payroll-period instance
/// (validated active). The credit's <c>concurrencyToken</c> travels in the <c>If-Match</c> header.
/// </summary>
public sealed record ApplyRecurringDeductionInstallmentRequest(
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes);

/// <summary>
/// Body for an EXTRAORDINARY payment (abono, RF-008): <c>Amount</c> goes 100 % against capital and SHORTENS the
/// term (the quota is untouched — P-04). Paying exactly the outstanding balance is a payoff and finalizes the
/// credit. Rejected on a SUSPENDIDO credit, above the balance, or on an indefinite plan.
/// </summary>
public sealed record ApplyRecurringDeductionExtraordinaryRequest(
    decimal Amount,
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes);

/// <summary>Body for annulling an applied charge (regular or extraordinary); the reason is mandatory.</summary>
public sealed record AnnulRecurringDeductionInstallmentRequest(string Reason);

/// <summary>
/// Body for the company-wide apply-period batch (RF-007): applies every due charge of the VIGENTE credits of
/// <c>PayrollTypeCode</c> up to the cutoff. Provide a <c>PayrollPeriodPublicId</c> (its end date is the cutoff and
/// its id/label are snapshotted) or a bare <c>CutoffDate</c>. <c>ExcludedDeductionPublicIds</c> postpones credits.
/// The batch is ATOMIC: any conflict rolls the whole run back (422).
/// </summary>
public sealed record ApplyRecurringDeductionPeriodRequest(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    IReadOnlyCollection<Guid>? ExcludedDeductionPublicIds);

/// <summary>
/// Body for the company-wide recurring-deduction bandeja query. Every filter is optional; when <c>StatusCode</c> is
/// omitted every status is listed (annulled / rejected included). The StatusCounts are always computed over every
/// status, and the per-currency totals cover the whole filtered set (not just the page).
/// </summary>
public sealed record QueryRecurringDeductionsRequest(
    Guid? EmployeeId,
    string? StatusCode,
    string? RecurringDeductionTypeCode,
    string? PayrollTypeCode,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    int? PageNumber,
    int? PageSize);

/// <summary>
/// Body for the company-wide pending-charges bandeja query. The cutoff is the <c>PayrollPeriodPublicId</c> end
/// date, the bare <c>CutoffDate</c>, or today; <c>StartDate</c> narrows the lower bound; <c>PayrollTypeCode</c> /
/// <c>EmployeeId</c> scope the VIGENTE-credit scan.
/// </summary>
public sealed record QueryPendingRecurringDeductionInstallmentsRequest(
    string? PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    DateOnly? StartDate,
    Guid? EmployeeId,
    int? PageNumber,
    int? PageSize);
