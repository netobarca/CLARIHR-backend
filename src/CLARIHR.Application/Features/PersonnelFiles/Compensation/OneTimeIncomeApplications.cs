using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Read DTOs ───────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One application of a one-time income (RF-011/RF-013). The payroll type, the (optional) payroll-period
/// imputation with its label, the origin (MANUAL / MOTOR / LIQUIDACION), the optional settlement reference and
/// the APLICADA → ANULADA lifecycle are the snapshot taken when the application was registered. At most one
/// active (APLICADA) application exists per income (RN-06). User ids are nullable (a non-Guid principal → null —
/// lesson REQ-003; the id serializes as <c>appliedByUserPublicId</c>).
/// </summary>
public sealed record OneTimeIncomeApplicationResponse(
    Guid ApplicationPublicId,
    DateOnly AppliedDate,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string? PayrollPeriodLabel,
    string OriginCode,
    string StatusCode,
    Guid? AppliedByUserId,
    Guid? SettlementPublicId,
    string? AnnulmentReason,
    Guid? AnnulledByUserId,
    DateTime? AnnulledUtc,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => ApplicationPublicId;
}

/// <summary>
/// The result of a UNITARY application / annulment: the affected application plus the one-time income's resulting
/// status and refreshed concurrency token (the income mutates — it becomes APLICADO when applied and reopens to
/// AUTORIZADO when the application is annulled). The <c>ETag</c> carries the income token.
/// </summary>
public sealed record OneTimeIncomeApplicationResult(
    OneTimeIncomeApplicationResponse Application,
    string OneTimeIncomeStatusCode,
    Guid OneTimeIncomeConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => Application.ApplicationPublicId;
}

/// <summary>The outcome of the company-wide apply-period batch: applied incomes and the number of postponed
/// incomes (excluded from the run although they were AUTORIZADO candidates).</summary>
public sealed record OneTimeIncomeApplyPeriodResult(
    int Aplicados,
    int Pospuestos);

/// <summary>
/// One pending-tray row (RF-012): an AUTORIZADO one-time income without an active application, with the employee
/// identity, the value + destination and the derived <see cref="IsOverdue"/> mark (its declared payroll-period end
/// date already passed). The income's <c>concurrencyToken</c> travels in the <c>If-Match</c> header of the unitary
/// application.
/// </summary>
public sealed record OneTimeIncomePendingRow(
    Guid OneTimeIncomePublicId,
    Guid PersonnelFilePublicId,
    string EmployeeName,
    DateOnly IncomeDate,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    decimal Amount,
    string CurrencyCode,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate,
    bool IsOverdue,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => OneTimeIncomePublicId;
}

/// <summary>The pending/overdue tray of the company's AUTORIZADO one-time incomes (RF-012) with the total + overdue counts.</summary>
public sealed record OneTimeIncomePendingResponse(
    IReadOnlyCollection<OneTimeIncomePendingRow> Items,
    int TotalCount,
    int OverdueCount);

// ── Repository projections (raw data assembled into responses by the handlers / repo) ────────────────

/// <summary>The resolution of a company payroll-period instance for an application imputation (§0.13, FK real);
/// null when it is not a period of the tenant.</summary>
public sealed record OneTimeIncomePayrollPeriodResolution(
    long InternalId,
    string Label,
    bool IsActive);

/// <summary>An AsNoTracking snapshot of one AUTORIZADO one-time income considered by the apply-period batch,
/// ordered by internal id (anti-deadlock ordering). Carries the income's own declared payroll-period destination
/// so an application defaults to it when the batch supplies no period override.</summary>
public sealed record OneTimeIncomeBatchCandidate(
    long InternalId,
    Guid PublicId,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel);

/// <summary>Raw pending-tray data of an AUTORIZADO one-time income (AsNoTracking); the handler adds the derived
/// <c>isOverdue</c> mark from the payroll-period end date.</summary>
public sealed record OneTimeIncomePendingData(
    Guid OneTimeIncomePublicId,
    Guid PersonnelFilePublicId,
    string EmployeeName,
    DateOnly IncomeDate,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    decimal Amount,
    string CurrencyCode,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate,
    Guid ConcurrencyToken);

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Registers the single application of an AUTORIZADO one-time income (RF-011). The amount does NOT travel (it is
/// the income's own <c>Amount</c>); <see cref="AppliedDate"/> defaults to today when omitted; the payroll period
/// (optional) defaults to the income's declared destination and, when a <see cref="PayrollPeriodPublicId"/> is
/// supplied, is validated + snapshotted against a company payroll-period instance (FK real, §0.13). The
/// <see cref="ConcurrencyToken"/> is the income's If-Match token.
/// </summary>
public sealed record ApplyOneTimeIncomeApplicationCommand(
    Guid PersonnelFileId,
    Guid OneTimeIncomePublicId,
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<OneTimeIncomeApplicationResult>;

/// <summary>Annuls the active application (RF-013); the reason is mandatory. The income reopens APLICADO →
/// AUTORIZADO so a new application can be registered. The <see cref="ConcurrencyToken"/> is the income's If-Match token.</summary>
public sealed record AnnulOneTimeIncomeApplicationCommand(
    Guid PersonnelFileId,
    Guid OneTimeIncomePublicId,
    Guid ApplicationPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OneTimeIncomeApplicationResult>;

/// <summary>
/// Applies, atomically, every AUTORIZADO one-time income of the company of a given payroll type (RF-012),
/// including the "atrasados" whose declared period already passed. The (optional) <see cref="PayrollPeriodPublicId"/>
/// (its id + label are snapshotted onto the applications, FK real) or <see cref="PayrollPeriodLabel"/> overrides the
/// destination for every applied income; otherwise each application defaults to its income's declared destination.
/// Excluded incomes are postponed. Any conflict rolls the whole batch back (422).
/// </summary>
public sealed record ApplyOneTimeIncomePeriodCommand(
    Guid CompanyId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string? PayrollPeriodLabel,
    IReadOnlyCollection<Guid> ExcludedIncomePublicIds)
    : ICommand<OneTimeIncomeApplyPeriodResult>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

/// <summary>The application history of a one-time income (active APLICADA + past ANULADA), most recent activity first.</summary>
public sealed record GetOneTimeIncomeApplicationsQuery(Guid PersonnelFileId, Guid OneTimeIncomePublicId)
    : IQuery<IReadOnlyCollection<OneTimeIncomeApplicationResponse>>;

/// <summary>The pending/overdue tray of the company's AUTORIZADO one-time incomes without an active application (RF-012).</summary>
public sealed record QueryOneTimeIncomePendingQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    bool OnlyOverdue)
    : IQuery<OneTimeIncomePendingResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class ApplyOneTimeIncomeApplicationCommandValidator : AbstractValidator<ApplyOneTimeIncomeApplicationCommand>
{
    public ApplyOneTimeIncomeApplicationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(PersonnelFileOneTimeIncomeApplication.MaxNotesLength);
    }
}

internal sealed class AnnulOneTimeIncomeApplicationCommandValidator : AbstractValidator<AnnulOneTimeIncomeApplicationCommand>
{
    public AnnulOneTimeIncomeApplicationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeIncomePublicId).NotEmpty();
        RuleFor(command => command.ApplicationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOneTimeIncomeApplication.MaxAnnulmentReasonLength);
    }
}

internal sealed class ApplyOneTimeIncomePeriodCommandValidator : AbstractValidator<ApplyOneTimeIncomePeriodCommand>
{
    public ApplyOneTimeIncomePeriodCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOneTimeIncome.MaxPayrollTypeCodeLength);
        RuleFor(command => command.PayrollPeriodLabel).MaximumLength(PersonnelFileOneTimeIncome.MaxPayrollPeriodLabelLength);
    }
}

internal sealed class GetOneTimeIncomeApplicationsQueryValidator : AbstractValidator<GetOneTimeIncomeApplicationsQuery>
{
    public GetOneTimeIncomeApplicationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OneTimeIncomePublicId).NotEmpty();
    }
}

internal sealed class QueryOneTimeIncomePendingQueryValidator : AbstractValidator<QueryOneTimeIncomePendingQuery>
{
    public QueryOneTimeIncomePendingQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayrollTypeCode).MaximumLength(PersonnelFileOneTimeIncome.MaxPayrollTypeCodeLength);
    }
}

// ── Handler-level errors (each needs an EN + ES resource entry — localization parity) ────────────────

/// <summary>
/// Dedicated errors for the application / annulment / apply-period slice (REQ-006 PR-4). The not-applicable /
/// already-applied / not-revertible codes are produced by the pure <see cref="Compensation.OneTimeIncomeRules"/>
/// and already localized (PR-2); these cover the handler-only guards (application not found, payroll period
/// invalid, and the atomic batch conflict with the offending detail). The annulment-reason-required code is
/// shared with the CRUD slice.
/// </summary>
internal static class OneTimeIncomeApplicationErrors
{
    public static readonly Error ApplicationNotFound = new(
        "ONE_TIME_INCOME_APPLICATION_NOT_FOUND",
        "The active application was not found on this one-time income.", ErrorType.NotFound);

    public static readonly Error PayrollPeriodInvalid = new(
        "ONE_TIME_INCOME_APPLICATION_PAYROLL_PERIOD_INVALID",
        "The payroll period is not a valid active period for the company.", ErrorType.UnprocessableEntity);

    /// <summary>The atomic apply-period batch hit a conflicting income (a concurrent run applied it or the income
    /// changed state); the whole batch is rolled back. The detail identifies the offending income.</summary>
    public static Error ApplyPeriodConflict(string detail) =>
        new(
            "ONE_TIME_INCOME_APPLY_PERIOD_CONFLICT",
            $"The apply-period batch could not be completed because of a conflicting one-time income: {detail}.",
            ErrorType.UnprocessableEntity,
            MessageArguments: [detail]);
}
