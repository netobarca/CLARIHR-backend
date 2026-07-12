using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Read DTOs ───────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One row of the theoretical CHARGE schedule of a recurring deduction (RF-006). It is a pure derivation of the
/// plan — never persisted. The rows advance by the APPLICATION cadence (a monthly quota charged fortnightly yields
/// two rows of half the quota — D-10) and skip the exception months (the plan is pushed forward, P-05). Applied
/// rows are marked <see cref="IsApplied"/>; an unapplied row whose due date is already past is
/// <see cref="IsOverdue"/> (a candidate for the next apply-period batch). The capital/interest split is present
/// only on a compound-interest credit.
/// </summary>
public sealed record RecurringDeductionScheduleItemResponse(
    int InstallmentNumber,
    DateOnly TheoreticalDueDate,
    decimal Amount,
    decimal? CapitalAmount,
    decimal? InterestAmount,
    bool IsApplied,
    bool IsOverdue);

/// <summary>
/// The derived schedule of a recurring deduction: the plan header + the theoretical charges (applied + projected +
/// overdue) + the extraordinary payments already made, with the derived totals the business asked for —
/// <see cref="TotalCharged"/> ("total cobrado") and <see cref="TotalOutstanding"/> ("total no cobrado"). For an
/// indefinite plan there is no total and no balance.
/// </summary>
public sealed record RecurringDeductionScheduleResponse(
    Guid RecurringDeductionPublicId,
    string StatusCode,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    string InstallmentFrequencyCode,
    string ApplicationFrequencyCode,
    DateOnly EffectiveDate,
    DateOnly InstallmentStartDate,
    IReadOnlyCollection<int> ExceptionMonths,
    int? InstallmentCount,
    int? ChargeCount,
    decimal? TotalAmount,
    decimal TotalCharged,
    decimal? TotalOutstanding,
    decimal? OutstandingBalance,
    bool IsPlanComplete,
    int NextInstallmentNumber,
    IReadOnlyCollection<RecurringDeductionScheduleItemResponse> Installments);

/// <summary>
/// One applied (or annulled) charge of a recurring deduction — REGULAR (a numbered charge of the plan) or
/// EXTRAORDINARIA (an out-of-sequence payoff payment, 100 % capital). The currency, payroll type and the (optional)
/// payroll-period imputation are snapshots taken when it was applied, as is the capital/interest split.
/// </summary>
public sealed record RecurringDeductionInstallmentResponse(
    Guid InstallmentPublicId,
    string Kind,
    int? InstallmentNumber,
    int? ExtraordinaryNumber,
    DateOnly AppliedDate,
    DateOnly? TheoreticalDueDate,
    decimal Amount,
    decimal? CapitalAmount,
    decimal? InterestAmount,
    string CurrencyCode,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string? PayrollPeriodLabel,
    string OriginCode,
    string StatusCode,
    Guid? AppliedByUserId,
    string? AnnulmentReason,
    Guid? AnnulledByUserId,
    DateTime? AnnulledUtc,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => InstallmentPublicId;
}

/// <summary>A page of a recurring deduction's charge history (APLICADA + ANULADA, most recent activity first).</summary>
public sealed record RecurringDeductionInstallmentHistoryResponse(
    IReadOnlyCollection<RecurringDeductionInstallmentResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

/// <summary>
/// The result of a UNITARY charge application / extraordinary payment / annulment: the affected row plus the
/// credit's resulting status and refreshed concurrency token (the credit mutates — it may finalize on the last
/// charge or on a payoff, and reopen when a completing charge is annulled). The ETag carries the credit's token.
/// </summary>
public sealed record RecurringDeductionInstallmentApplicationResult(
    RecurringDeductionInstallmentResponse Installment,
    string RecurringDeductionStatusCode,
    Guid RecurringDeductionConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => Installment.InstallmentPublicId;
}

/// <summary>The outcome of the company-wide apply-period batch: charges applied, credits finalized, and the number
/// of postponed credits (excluded from the run although they had a due charge).</summary>
public sealed record RecurringDeductionApplyPeriodResult(
    int Aplicadas,
    int Finalizados,
    int Pospuestas);

// ── Repository projections ──────────────────────────────────────────────────────────────────────────

/// <summary>Raw plan + applied data of a recurring deduction used to build the derived schedule.</summary>
public sealed record RecurringDeductionScheduleData(
    Guid PublicId,
    string StatusCode,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    string InstallmentFrequencyCode,
    string ApplicationFrequencyCode,
    DateOnly EffectiveDate,
    DateOnly InstallmentStartDate,
    IReadOnlyCollection<int> ExceptionMonths,
    decimal? PrincipalAmount,
    decimal? InterestRatePercent,
    int? PlannedInstallments,
    IReadOnlyCollection<RecurringDeductionSegment> Segments,
    IReadOnlyCollection<int> AppliedInstallmentNumbers,
    decimal ChargedAmount,
    decimal ChargedCapital);

/// <summary>The resolution of a company payroll-period instance for a charge imputation (FK real).</summary>
public sealed record RecurringDeductionPayrollPeriodResolution(
    long InternalId,
    string Label,
    DateOnly EndDate,
    bool IsActive);

/// <summary>An AsNoTracking snapshot of one VIGENTE recurring deduction considered by the apply-period batch.</summary>
public sealed record RecurringDeductionBatchScanItem(
    long InternalId,
    Guid PublicId,
    DateOnly EffectiveDate,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    string InstallmentFrequencyCode,
    string ApplicationFrequencyCode,
    DateOnly InstallmentStartDate,
    IReadOnlyCollection<int> ExceptionMonths,
    decimal? PrincipalAmount,
    decimal? InterestRatePercent,
    int? PlannedInstallments,
    IReadOnlyCollection<RecurringDeductionSegment> Segments,
    IReadOnlyCollection<int> AppliedInstallmentNumbers);

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Applies the NEXT charge of a VIGENTE recurring deduction by hand (RF-006). The amount and its capital/interest
/// split are computed by the rules and are NOT editable; the payroll period (optional) is validated + snapshotted.
/// The <see cref="ConcurrencyToken"/> is the credit's If-Match token.
/// </summary>
public sealed record ApplyRecurringDeductionInstallmentCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<RecurringDeductionInstallmentApplicationResult>;

/// <summary>
/// Applies an EXTRAORDINARY payment (abono, RF-008): 100 % capital, it shortens the plan (P-04 — the quota is not
/// touched, the term is). Paying exactly the outstanding balance is a payoff and finalizes the credit. Never on a
/// SUSPENDIDO credit, never above the balance, never on an indefinite plan.
/// </summary>
public sealed record ApplyRecurringDeductionExtraordinaryCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    decimal Amount,
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<RecurringDeductionInstallmentApplicationResult>;

/// <summary>Annuls an applied charge (regular or extraordinary); the reason is mandatory. Reopens FINALIZADO →
/// VIGENTE when the plan is no longer complete. The <see cref="ConcurrencyToken"/> is the credit's If-Match token.</summary>
public sealed record AnnulRecurringDeductionInstallmentCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    Guid InstallmentPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<RecurringDeductionInstallmentApplicationResult>;

/// <summary>
/// Applies, atomically, every due charge of the company's VIGENTE recurring deductions of a given payroll type up
/// to the period cutoff (RF-007). The cutoff is the payroll period's end date (when
/// <see cref="PayrollPeriodPublicId"/> is supplied, its id + label are snapshotted onto the charges) or the bare
/// <see cref="CutoffDate"/>. Excluded credits are postponed. Any conflict rolls the whole batch back (422).
/// </summary>
public sealed record ApplyRecurringDeductionPeriodCommand(
    Guid CompanyId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    IReadOnlyCollection<Guid> ExcludedDeductionPublicIds)
    : ICommand<RecurringDeductionApplyPeriodResult>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

public sealed record GetRecurringDeductionScheduleQuery(Guid PersonnelFileId, Guid RecurringDeductionPublicId)
    : IQuery<RecurringDeductionScheduleResponse>;

public sealed record GetRecurringDeductionInstallmentsQuery(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    int PageNumber,
    int PageSize)
    : IQuery<RecurringDeductionInstallmentHistoryResponse>;

// ── Errors ────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Handler-level errors of the charge application slice (each code has an EN + ES resource entry).</summary>
internal static class RecurringDeductionInstallmentErrors
{
    public static readonly Error InstallmentNotFound = new(
        "RECURRING_DEDUCTION_INSTALLMENT_NOT_FOUND",
        "The installment was not found on this recurring deduction.", ErrorType.NotFound);

    public static readonly Error PayrollPeriodInvalid = new(
        "RECURRING_DEDUCTION_INSTALLMENT_PAYROLL_PERIOD_INVALID",
        "The payroll period is not a valid active period for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "RECURRING_DEDUCTION_INSTALLMENT_ANNULMENT_REASON_REQUIRED",
        "An annulment reason is required to annul an installment.", ErrorType.UnprocessableEntity);

    public static readonly Error PlanComplete = new(
        "RECURRING_DEDUCTION_INSTALLMENT_PLAN_COMPLETE",
        "The installment plan is already complete; there are no more installments to apply.", ErrorType.UnprocessableEntity);

    public static readonly Error NotApplicable = new(
        "RECURRING_DEDUCTION_INSTALLMENT_NOT_APPLICABLE",
        "Only a VIGENTE recurring deduction can apply installments.", ErrorType.UnprocessableEntity);

    /// <summary>The apply-period batch is ATOMIC: any conflict rolls the whole run back with the offender in the detail.</summary>
    public static Error ApplyPeriodConflict(string detail) => new(
        "RECURRING_DEDUCTION_APPLY_PERIOD_CONFLICT",
        $"The apply-period batch was rolled back because of a conflict: {detail}.", ErrorType.UnprocessableEntity);
}

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class ApplyRecurringDeductionInstallmentCommandValidator : AbstractValidator<ApplyRecurringDeductionInstallmentCommand>
{
    public ApplyRecurringDeductionInstallmentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(PersonnelFileRecurringDeductionInstallment.MaxNotesLength);
    }
}

internal sealed class ApplyRecurringDeductionExtraordinaryCommandValidator : AbstractValidator<ApplyRecurringDeductionExtraordinaryCommand>
{
    public ApplyRecurringDeductionExtraordinaryCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Amount)
            .GreaterThan(0m)
            .WithMessage("The extraordinary payment must be greater than zero.");
        RuleFor(command => command.Notes).MaximumLength(PersonnelFileRecurringDeductionInstallment.MaxNotesLength);
    }
}

internal sealed class AnnulRecurringDeductionInstallmentCommandValidator : AbstractValidator<AnnulRecurringDeductionInstallmentCommand>
{
    public AnnulRecurringDeductionInstallmentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.InstallmentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileRecurringDeductionInstallment.MaxAnnulmentReasonLength);
    }
}

internal sealed class ApplyRecurringDeductionPeriodCommandValidator : AbstractValidator<ApplyRecurringDeductionPeriodCommand>
{
    public ApplyRecurringDeductionPeriodCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileRecurringDeduction.MaxPayrollTypeCodeLength);

        // The batch needs a cutoff: either a payroll period (whose end date is the cutoff) or a bare date.
        RuleFor(command => command)
            .Must(command => (command.PayrollPeriodPublicId is { } id && id != Guid.Empty) || command.CutoffDate.HasValue)
            .WithMessage("The apply-period batch requires either a payroll period or a cutoff date.");
    }
}

internal sealed class GetRecurringDeductionScheduleQueryValidator : AbstractValidator<GetRecurringDeductionScheduleQuery>
{
    public GetRecurringDeductionScheduleQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecurringDeductionPublicId).NotEmpty();
    }
}

internal sealed class GetRecurringDeductionInstallmentsQueryValidator : AbstractValidator<GetRecurringDeductionInstallmentsQuery>
{
    public GetRecurringDeductionInstallmentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecurringDeductionPublicId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}
