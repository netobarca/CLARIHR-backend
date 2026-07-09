using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Read DTOs ───────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One row of the theoretical installment schedule of a recurring income (REQ-005 RF-006/D-07). It is a pure
/// derivation of the plan (<c>RecurringIncomeRules.BuildProjection</c>) — never persisted: applied installments
/// are marked <see cref="IsApplied"/>; an unapplied installment whose theoretical due date is already past is
/// <see cref="IsOverdue"/> (a candidate for the next apply-period batch).
/// </summary>
public sealed record RecurringIncomeScheduleItemResponse(
    int InstallmentNumber,
    DateOnly TheoreticalDueDate,
    decimal Amount,
    bool IsApplied,
    bool IsOverdue);

/// <summary>
/// The derived schedule of a recurring income: the plan header + the theoretical installments (applied +
/// projected + overdue) with the running balance / completion. For an indefinite plan the schedule projects a
/// rolling horizon and there is no <see cref="RemainingAmount"/> (P-06).
/// </summary>
public sealed record RecurringIncomeScheduleResponse(
    Guid RecurringIncomePublicId,
    string StatusCode,
    bool IsIndefinite,
    string InstallmentFrequencyCode,
    DateOnly InstallmentStartDate,
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    decimal? RemainingAmount,
    bool IsPlanComplete,
    int NextInstallmentNumber,
    IReadOnlyCollection<RecurringIncomeScheduleItemResponse> Installments);

/// <summary>
/// One applied (or annulled) installment of a recurring income (RF-006/RF-008). The currency, payroll type and
/// the (optional) payroll-period imputation are snapshots taken when the installment was applied. The
/// <see cref="PayrollPeriodPublicId"/> is the public id of the imputed company payroll period (null when the
/// installment was applied against a bare date range).
/// </summary>
public sealed record RecurringIncomeInstallmentResponse(
    Guid InstallmentPublicId,
    int InstallmentNumber,
    DateOnly AppliedDate,
    DateOnly TheoreticalDueDate,
    decimal Amount,
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

/// <summary>A page of a recurring income's installment history (APLICADA + ANULADA, most recent activity first).</summary>
public sealed record RecurringIncomeInstallmentHistoryResponse(
    IReadOnlyCollection<RecurringIncomeInstallmentResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

/// <summary>
/// The result of a UNITARY installment application / annulment: the affected installment plus the recurring
/// income's resulting status and refreshed concurrency token (the income mutates — it may finalize on the last
/// installment or reopen when a completing installment is annulled). The ETag carries the income token.
/// </summary>
public sealed record RecurringIncomeInstallmentApplicationResult(
    RecurringIncomeInstallmentResponse Installment,
    string RecurringIncomeStatusCode,
    Guid RecurringIncomeConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => Installment.InstallmentPublicId;
}

/// <summary>The outcome of the company-wide apply-period batch: applied installments, incomes finalized, and the
/// number of postponed incomes (excluded from the run although they had a due installment).</summary>
public sealed record RecurringIncomeApplyPeriodResult(
    int Aplicadas,
    int Finalizados,
    int Pospuestas);

// ── Repository projections (raw data assembled into responses by the handlers / repo) ────────────────

/// <summary>Raw plan + applied-number data of a recurring income used to build the derived schedule.</summary>
public sealed record RecurringIncomeScheduleData(
    Guid PublicId,
    string StatusCode,
    bool IsIndefinite,
    string InstallmentFrequencyCode,
    DateOnly InstallmentStartDate,
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    IReadOnlyCollection<int> AppliedInstallmentNumbers);

/// <summary>The resolution of a company payroll-period instance for an installment imputation (§0.13, FK real).</summary>
public sealed record RecurringIncomePayrollPeriodResolution(
    long InternalId,
    string Label,
    DateOnly EndDate,
    bool IsActive);

/// <summary>An AsNoTracking snapshot of one VIGENTE recurring income considered by the apply-period batch.</summary>
public sealed record RecurringIncomeBatchScanItem(
    long InternalId,
    Guid PublicId,
    bool IsIndefinite,
    string InstallmentFrequencyCode,
    DateOnly InstallmentStartDate,
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    IReadOnlyCollection<int> AppliedInstallmentNumbers);

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Applies the NEXT installment of a VIGENTE recurring income by hand (RF-006). The amount is computed by the
/// rules and is NOT editable (P-04); the payroll period (optional) is validated + snapshotted. The
/// <see cref="ConcurrencyToken"/> is the income's If-Match token.
/// </summary>
public sealed record ApplyRecurringIncomeInstallmentCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<RecurringIncomeInstallmentApplicationResult>;

/// <summary>Annuls an applied installment (RF-008); the reason is mandatory. Reopens FINALIZADO → VIGENTE when the
/// plan is no longer complete. The <see cref="ConcurrencyToken"/> is the income's If-Match token.</summary>
public sealed record AnnulRecurringIncomeInstallmentCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    Guid InstallmentPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<RecurringIncomeInstallmentApplicationResult>;

/// <summary>
/// Applies, atomically, every due installment of the company's VIGENTE recurring incomes of a given payroll type
/// up to the period cutoff (RF-007). The cutoff is the payroll period's end date (when
/// <see cref="PayrollPeriodPublicId"/> is supplied, its id + label are snapshotted onto the installments) or the
/// bare <see cref="CutoffDate"/>. Excluded incomes are postponed. Any conflict rolls the whole batch back (422).
/// </summary>
public sealed record ApplyRecurringIncomePeriodCommand(
    Guid CompanyId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    IReadOnlyCollection<Guid> ExcludedIncomePublicIds)
    : ICommand<RecurringIncomeApplyPeriodResult>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

public sealed record GetRecurringIncomeScheduleQuery(Guid PersonnelFileId, Guid RecurringIncomePublicId)
    : IQuery<RecurringIncomeScheduleResponse>;

public sealed record GetRecurringIncomeInstallmentsQuery(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    int PageNumber,
    int PageSize)
    : IQuery<RecurringIncomeInstallmentHistoryResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class ApplyRecurringIncomeInstallmentCommandValidator : AbstractValidator<ApplyRecurringIncomeInstallmentCommand>
{
    public ApplyRecurringIncomeInstallmentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(PersonnelFileRecurringIncomeInstallment.MaxNotesLength);
    }
}

internal sealed class AnnulRecurringIncomeInstallmentCommandValidator : AbstractValidator<AnnulRecurringIncomeInstallmentCommand>
{
    public AnnulRecurringIncomeInstallmentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.InstallmentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileRecurringIncomeInstallment.MaxAnnulmentReasonLength);
    }
}

internal sealed class ApplyRecurringIncomePeriodCommandValidator : AbstractValidator<ApplyRecurringIncomePeriodCommand>
{
    public ApplyRecurringIncomePeriodCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileRecurringIncome.MaxPayrollTypeCodeLength);

        // The batch needs a cutoff: either a payroll-period instance (its end date wins) or a bare cutoff date.
        RuleFor(command => command)
            .Must(command => command.PayrollPeriodPublicId is not null || command.CutoffDate is not null)
            .WithMessage("Either a payroll period or a cutoff date is required for the apply-period batch.");
    }
}

internal sealed class GetRecurringIncomeScheduleQueryValidator : AbstractValidator<GetRecurringIncomeScheduleQuery>
{
    public GetRecurringIncomeScheduleQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecurringIncomePublicId).NotEmpty();
    }
}

internal sealed class GetRecurringIncomeInstallmentsQueryValidator : AbstractValidator<GetRecurringIncomeInstallmentsQuery>
{
    public GetRecurringIncomeInstallmentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecurringIncomePublicId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

// ── Handler-level errors (each needs an EN + ES resource entry — localization parity) ────────────────

/// <summary>
/// Dedicated errors for the installment application / annulment / apply-period slice (REQ-005 PR-4). The strict
/// sequence / not-applicable / exceeds-plan codes are produced by the pure <see cref="RecurringIncomeRules"/> and
/// already localized; these cover the handler-only guards (installment not found, payroll period invalid,
/// annulment reason required, plan already complete, and the atomic batch conflict with the offending detail).
/// </summary>
internal static class RecurringIncomeInstallmentErrors
{
    public static readonly Error InstallmentNotFound = new(
        "RECURRING_INCOME_INSTALLMENT_NOT_FOUND",
        "The installment was not found on this recurring income.", ErrorType.NotFound);

    public static readonly Error PayrollPeriodInvalid = new(
        "RECURRING_INCOME_INSTALLMENT_PAYROLL_PERIOD_INVALID",
        "The payroll period is not a valid active period for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "RECURRING_INCOME_INSTALLMENT_ANNULMENT_REASON_REQUIRED",
        "An annulment reason is required to annul an installment.", ErrorType.UnprocessableEntity);

    public static readonly Error PlanComplete = new(
        "RECURRING_INCOME_INSTALLMENT_PLAN_COMPLETE",
        "The installment plan is already complete; there are no more installments to apply.", ErrorType.UnprocessableEntity);

    /// <summary>The income is not VIGENTE, so no installment can be applied (shares the pure rule's code).</summary>
    public static readonly Error NotApplicable = new(
        RecurringIncomeRules.InstallmentNotApplicableCode,
        "Only a VIGENTE recurring income can apply installments.", ErrorType.UnprocessableEntity);

    /// <summary>The atomic apply-period batch hit a conflicting installment (a concurrent run applied it or the
    /// income changed state); the whole batch is rolled back. The detail identifies the offending installment.</summary>
    public static Error ApplyPeriodConflict(string detail) =>
        new(
            "RECURRING_INCOME_APPLY_PERIOD_CONFLICT",
            $"The apply-period batch could not be completed because of a conflicting installment: {detail}.",
            ErrorType.UnprocessableEntity,
            MessageArguments: [detail]);
}
