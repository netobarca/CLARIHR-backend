using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Read DTOs ───────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One application of an overtime record (RF-011/RF-013). The payroll type, the (optional) payroll-period
/// imputation with its label, the origin (MANUAL / MOTOR / LIQUIDACION), the optional settlement reference and the
/// APLICADA → ANULADA lifecycle are the snapshot taken when the application was registered. At most one active
/// (APLICADA) application exists per record (RN-06). User ids are nullable (a non-Guid principal → null — lesson
/// REQ-003; the id serializes as <c>appliedByUserPublicId</c>).
/// </summary>
public sealed record OvertimeRecordApplicationResponse(
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
/// The result of a UNITARY application / annulment: the affected application plus the overtime record's resulting
/// status and refreshed concurrency token (the record mutates — it becomes APLICADA when applied and reopens to
/// AUTORIZADA when the application is annulled). The <c>ETag</c> carries the record token.
/// </summary>
public sealed record OvertimeRecordApplicationResult(
    OvertimeRecordApplicationResponse Application,
    string OvertimeRecordStatusCode,
    Guid OvertimeRecordConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => Application.ApplicationPublicId;
}

/// <summary>The outcome of the company-wide apply-period batch: the number of applied records and the number of
/// postponed records (excluded from the run although they were AUTORIZADA candidates whose work date has elapsed).</summary>
public sealed record OvertimeApplyPeriodResult(
    int Aplicados,
    int Pospuestos);

/// <summary>
/// One pending-tray row (RF-012): an AUTORIZADA overtime record without an active application, with the employee
/// identity, the shift + destination and the derived <see cref="IsOverdue"/> mark (its declared payroll-period end
/// date already passed). The record's <c>concurrencyToken</c> travels in the <c>If-Match</c> header of the unitary
/// application.
/// </summary>
public sealed record OvertimeRecordPendingRow(
    Guid OvertimeRecordPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeName,
    DateOnly WorkDate,
    string OvertimeTypeCodeSnapshot,
    string OvertimeTypeNameSnapshot,
    decimal DurationDecimalHours,
    decimal FactorApplied,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate,
    bool IsOverdue,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => OvertimeRecordPublicId;
}

/// <summary>The pending/overdue tray of the company's AUTORIZADA overtime records (RF-012) with the total + overdue counts.</summary>
public sealed record OvertimeRecordPendingResponse(
    IReadOnlyCollection<OvertimeRecordPendingRow> Items,
    int TotalCount,
    int OverdueCount);

// ── Repository projections (raw data assembled into responses by the handlers / repo) ────────────────

/// <summary>The resolution of a company payroll-period instance for an application imputation (§0.14, FK real);
/// null when it is not a period of the tenant.</summary>
public sealed record OvertimePayrollPeriodResolution(
    long InternalId,
    string Label,
    bool IsActive);

/// <summary>An AsNoTracking snapshot of one AUTORIZADA overtime record considered by the apply-period batch
/// (its work date has elapsed), ordered by internal id (anti-deadlock ordering). Carries the record's own declared
/// payroll-period destination so an application defaults to it when the batch supplies no period override.</summary>
public sealed record OvertimeApplyPeriodCandidate(
    long InternalId,
    Guid PublicId,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel);

/// <summary>Raw pending-tray data of an AUTORIZADA overtime record (AsNoTracking); the handler adds the derived
/// <c>isOverdue</c> mark from the payroll-period end date.</summary>
public sealed record OvertimePendingData(
    Guid OvertimeRecordPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeName,
    DateOnly WorkDate,
    string OvertimeTypeCodeSnapshot,
    string OvertimeTypeNameSnapshot,
    decimal DurationDecimalHours,
    decimal FactorApplied,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate,
    Guid ConcurrencyToken);

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Registers the single application of an AUTORIZADA overtime record (RF-011). The hours do NOT travel (they are
/// the record's own duration/factor); <see cref="AppliedDate"/> defaults to today when omitted; the record's work
/// date must have elapsed (a future organized shift is not payable, №13 — 422 <c>OVERTIME_WORK_DATE_NOT_ELAPSED</c>);
/// the payroll period (optional) defaults to the record's declared destination and, when a
/// <see cref="PayrollPeriodPublicId"/> is supplied, is validated + snapshotted against a company payroll-period
/// instance (FK real, §0.14). The <see cref="ConcurrencyToken"/> is the record's If-Match token.
/// </summary>
public sealed record ApplyOvertimeRecordApplicationCommand(
    Guid PersonnelFileId,
    Guid OvertimeRecordPublicId,
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<OvertimeRecordApplicationResult>;

/// <summary>Annuls the active application (RF-013); the reason is mandatory. The record reopens APLICADA →
/// AUTORIZADA so a new application can be registered. The <see cref="ConcurrencyToken"/> is the record's If-Match token.</summary>
public sealed record AnnulOvertimeRecordApplicationCommand(
    Guid PersonnelFileId,
    Guid OvertimeRecordPublicId,
    Guid ApplicationPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OvertimeRecordApplicationResult>;

/// <summary>
/// Applies, atomically, every AUTORIZADA overtime record of the company of a given payroll type whose work date has
/// elapsed (RF-012), including the "atrasados" whose declared period already passed; future organized shifts are
/// excluded. The (optional) <see cref="PayrollPeriodPublicId"/> (its id + label are snapshotted onto the
/// applications, FK real) or <see cref="PayrollPeriodLabel"/> overrides the destination for every applied record;
/// otherwise each application defaults to its record's declared destination. Excluded records are postponed. Any
/// conflict rolls the whole batch back (422).
/// </summary>
public sealed record ApplyOvertimePeriodCommand(
    Guid CompanyId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string? PayrollPeriodLabel,
    IReadOnlyCollection<Guid> ExcludedRecordPublicIds)
    : ICommand<OvertimeApplyPeriodResult>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

/// <summary>The application history of an overtime record (active APLICADA + past ANULADA), most recent activity first.</summary>
public sealed record GetOvertimeRecordApplicationsQuery(Guid PersonnelFileId, Guid OvertimeRecordPublicId)
    : IQuery<IReadOnlyCollection<OvertimeRecordApplicationResponse>>;

/// <summary>The pending/overdue tray of the company's AUTORIZADA overtime records without an active application (RF-012).</summary>
public sealed record QueryOvertimeRecordPendingQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    bool OnlyOverdue)
    : IQuery<OvertimeRecordPendingResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class ApplyOvertimeRecordApplicationCommandValidator : AbstractValidator<ApplyOvertimeRecordApplicationCommand>
{
    public ApplyOvertimeRecordApplicationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OvertimeRecordPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(PersonnelFileOvertimeRecordApplication.MaxNotesLength);
    }
}

internal sealed class AnnulOvertimeRecordApplicationCommandValidator : AbstractValidator<AnnulOvertimeRecordApplicationCommand>
{
    public AnnulOvertimeRecordApplicationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OvertimeRecordPublicId).NotEmpty();
        RuleFor(command => command.ApplicationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOvertimeRecordApplication.MaxAnnulmentReasonLength);
    }
}

internal sealed class ApplyOvertimePeriodCommandValidator : AbstractValidator<ApplyOvertimePeriodCommand>
{
    public ApplyOvertimePeriodCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOvertimeRecord.MaxPayrollTypeCodeLength);
        RuleFor(command => command.PayrollPeriodLabel).MaximumLength(PersonnelFileOvertimeRecord.MaxPayrollPeriodLabelLength);
    }
}

internal sealed class GetOvertimeRecordApplicationsQueryValidator : AbstractValidator<GetOvertimeRecordApplicationsQuery>
{
    public GetOvertimeRecordApplicationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OvertimeRecordPublicId).NotEmpty();
    }
}

internal sealed class QueryOvertimeRecordPendingQueryValidator : AbstractValidator<QueryOvertimeRecordPendingQuery>
{
    public QueryOvertimeRecordPendingQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayrollTypeCode).MaximumLength(PersonnelFileOvertimeRecord.MaxPayrollTypeCodeLength);
    }
}

// ── Handler-level errors (each needs an EN + ES resource entry — localization parity) ────────────────

/// <summary>
/// Dedicated errors for the application / annulment / apply-period slice (REQ-007 PR-4). The not-applicable /
/// already-applied / work-date-not-elapsed / not-revertible codes are produced by the pure
/// <see cref="OvertimeRecordRules"/> and already localized (PR-3); the payroll-type-invalid + annulment-reason
/// codes are shared with the CRUD/resolution slice (<see cref="OvertimeRecordErrors"/>). These cover the
/// handler-only guards (application not found, payroll period invalid, and the atomic batch conflict with the
/// offending detail).
/// </summary>
internal static class OvertimeRecordApplicationErrors
{
    public static readonly Error ApplicationNotFound = new(
        "OVERTIME_APPLICATION_NOT_FOUND",
        "The active application was not found on this overtime record.", ErrorType.NotFound);

    public static readonly Error PayrollPeriodInvalid = new(
        "OVERTIME_APPLICATION_PAYROLL_PERIOD_INVALID",
        "The payroll period is not a valid active period for the company.", ErrorType.UnprocessableEntity);

    /// <summary>The atomic apply-period batch hit a conflicting record (a concurrent run applied it or the record
    /// changed state); the whole batch is rolled back. The detail identifies the offending record.</summary>
    public static Error ApplyPeriodConflict(string detail) =>
        new(
            "OVERTIME_APPLY_PERIOD_CONFLICT",
            $"The apply-period batch could not be completed because of a conflicting overtime record: {detail}.",
            ErrorType.UnprocessableEntity,
            MessageArguments: [detail]);
}
