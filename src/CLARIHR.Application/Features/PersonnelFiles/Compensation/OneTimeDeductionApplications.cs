using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Read DTOs ───────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The single application of a one-time deduction — the moment the charge actually lands on a payroll. It
/// snapshots the payroll type and period, the origin (MANUAL / MOTOR / LIQUIDACION) and the APLICADA → ANULADA
/// lifecycle. Annulling it is the REVERSAL: the deduction returns to AUTORIZADO and can be charged again.
/// </summary>
public sealed record OneTimeDeductionApplicationResponse(
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

/// <summary>The result of applying or reverting: the affected application plus the deduction's resulting status
/// and refreshed token (the deduction mutates — APLICADO on apply, back to AUTORIZADO on reversal).</summary>
public sealed record OneTimeDeductionApplicationResult(
    OneTimeDeductionApplicationResponse Application,
    string OneTimeDeductionStatusCode,
    Guid OneTimeDeductionConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => Application.ApplicationPublicId;
}

/// <summary>The outcome of the company-wide apply-period batch: deductions charged and deductions postponed.</summary>
public sealed record OneTimeDeductionApplyPeriodResult(
    int Aplicados,
    int Pospuestos);

/// <summary>One AUTORIZADO deduction still waiting to be charged (the work list of the payroll operator).</summary>
public sealed record OneTimeDeductionPendingRow(
    Guid OneTimeDeductionPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeName,
    DateOnly DeductionDate,
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
    public Guid Id => OneTimeDeductionPublicId;
}

public sealed record OneTimeDeductionPendingResponse(
    IReadOnlyCollection<OneTimeDeductionPendingRow> Items,
    int TotalCount,
    int OverdueCount);

// ── Repository projections ──────────────────────────────────────────────────────────────────────────

public sealed record OneTimeDeductionPayrollPeriodResolution(
    long InternalId,
    string Label,
    bool IsActive);

/// <summary>An AUTORIZADO deduction the apply-period batch will charge (ordered by internal id — anti-deadlock).</summary>
public sealed record OneTimeDeductionBatchCandidate(
    long InternalId,
    Guid PublicId,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel);

public sealed record OneTimeDeductionPendingData(
    Guid OneTimeDeductionPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeName,
    DateOnly DeductionDate,
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
/// Charges the deduction (the single application). It must be AUTORIZADO and must not already carry an active
/// application. <see cref="ConcurrencyToken"/> is the deduction's If-Match token.
/// </summary>
public sealed record ApplyOneTimeDeductionApplicationCommand(
    Guid PersonnelFileId,
    Guid OneTimeDeductionPublicId,
    DateOnly? AppliedDate,
    Guid? PayrollPeriodPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<OneTimeDeductionApplicationResult>;

/// <summary>
/// Annuls the active application — THE REVERSAL. The reason is mandatory; the deduction returns to AUTORIZADO and
/// the filtered-unique index frees the slot so it can be charged again.
/// </summary>
public sealed record AnnulOneTimeDeductionApplicationCommand(
    Guid PersonnelFileId,
    Guid OneTimeDeductionPublicId,
    Guid ApplicationPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OneTimeDeductionApplicationResult>;

/// <summary>
/// Charges, atomically, every AUTORIZADO deduction of the company targeting the given payroll type (and, when
/// supplied, the given payroll period). Excluded deductions are postponed. Any conflict rolls the whole batch back.
/// </summary>
public sealed record ApplyOneTimeDeductionPeriodCommand(
    Guid CompanyId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    IReadOnlyCollection<Guid> ExcludedDeductionPublicIds)
    : ICommand<OneTimeDeductionApplyPeriodResult>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

public sealed record GetOneTimeDeductionApplicationsQuery(Guid PersonnelFileId, Guid OneTimeDeductionPublicId)
    : IQuery<IReadOnlyCollection<OneTimeDeductionApplicationResponse>>;

/// <summary>The company-wide work list: the AUTORIZADO deductions still waiting to be charged.</summary>
public sealed record QueryOneTimeDeductionPendingQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    Guid? EmployeeId)
    : IQuery<OneTimeDeductionPendingResponse>;

// ── Errors ────────────────────────────────────────────────────────────────────────────────────────

internal static class OneTimeDeductionApplicationErrors
{
    public static readonly Error ApplicationNotFound = new(
        "ONE_TIME_DEDUCTION_APPLICATION_NOT_FOUND",
        "The application was not found on this one-time deduction.", ErrorType.NotFound);

    public static readonly Error AnnulmentReasonRequired = new(
        "ONE_TIME_DEDUCTION_APPLICATION_ANNULMENT_REASON_REQUIRED",
        "An annulment reason is required to revert an application.", ErrorType.UnprocessableEntity);

    /// <summary>The apply-period batch is ATOMIC: any conflict rolls the whole run back with the offender in the detail.</summary>
    public static Error ApplyPeriodConflict(string detail) => new(
        "ONE_TIME_DEDUCTION_APPLY_PERIOD_CONFLICT",
        $"The apply-period batch was rolled back because of a conflict: {detail}.", ErrorType.UnprocessableEntity);
}

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class ApplyOneTimeDeductionApplicationCommandValidator : AbstractValidator<ApplyOneTimeDeductionApplicationCommand>
{
    public ApplyOneTimeDeductionApplicationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(PersonnelFileOneTimeDeductionApplication.MaxNotesLength);
    }
}

internal sealed class AnnulOneTimeDeductionApplicationCommandValidator : AbstractValidator<AnnulOneTimeDeductionApplicationCommand>
{
    public AnnulOneTimeDeductionApplicationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeDeductionPublicId).NotEmpty();
        RuleFor(command => command.ApplicationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOneTimeDeductionApplication.MaxAnnulmentReasonLength);
    }
}

internal sealed class ApplyOneTimeDeductionPeriodCommandValidator : AbstractValidator<ApplyOneTimeDeductionPeriodCommand>
{
    public ApplyOneTimeDeductionPeriodCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOneTimeDeduction.MaxPayrollTypeCodeLength);
    }
}

internal sealed class GetOneTimeDeductionApplicationsQueryValidator : AbstractValidator<GetOneTimeDeductionApplicationsQuery>
{
    public GetOneTimeDeductionApplicationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OneTimeDeductionPublicId).NotEmpty();
    }
}

internal sealed class QueryOneTimeDeductionPendingQueryValidator : AbstractValidator<QueryOneTimeDeductionPendingQuery>
{
    public QueryOneTimeDeductionPendingQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}
