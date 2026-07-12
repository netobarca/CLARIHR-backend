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
