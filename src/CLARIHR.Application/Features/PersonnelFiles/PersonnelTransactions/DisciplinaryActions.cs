using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>
/// A disciplinary action ("amonestación") with its one-decision lifecycle (REQ-003 D-02/D-08). A fault
/// registered on the employee file that, once authorized, is applied with an automatic <c>AMONESTACION</c>
/// personnel-action entry (plus a <c>SUSPENSION</c> entry when the unpaid-suspension block applies). The
/// optional payroll-deduction block carries an amount and a concept frozen at Apply (aclaración №5). The
/// self-service employee only ever sees their APLICADA disciplinary actions (D-13).
/// </summary>
public sealed record PersonnelFileDisciplinaryActionResponse(
    Guid DisciplinaryActionPublicId,
    Guid DisciplinaryActionTypePublicId,
    string TypeNameSnapshot,
    bool TypeAppliedSuspension,
    Guid DisciplinaryActionCausePublicId,
    string CauseNameSnapshot,
    DateOnly IncidentDate,
    string FactsDetail,
    bool HasPayrollDeduction,
    decimal? DeductionAmount,
    string? CurrencyCode,
    string? DeductionConceptTypeCode,
    string? DeductionConceptNameSnapshot,
    DateOnly? SuspensionStartDate,
    DateOnly? SuspensionEndDate,
    int? SuspensionDays,
    Guid? AssignedPositionPublicId,
    string RegisteredByUserId,
    string StatusCode,
    string? DecidedByUserId,
    DateTime? DecidedUtc,
    string? DecisionNote,
    string? AnnulmentReason,
    string? AnnulledByUserId,
    DateTime? AnnulledUtc,
    Guid? PersonnelActionPublicId,
    Guid? SuspensionActionPublicId,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc)
{
    [JsonIgnore]
    public Guid Id => DisciplinaryActionPublicId;
}

/// <summary>
/// Declarative fields for registering or editing a disciplinary action. The type and cause travel as public ids.
/// The suspension block (<see cref="SuspensionStartDate"/>/<see cref="SuspensionEndDate"/>) is only allowed on
/// types that apply suspension (RN-05); the deduction block (<see cref="HasPayrollDeduction"/> +
/// <see cref="DeductionAmount"/>) requires a positive amount when set (RN-06). The optional
/// <see cref="DeductionConceptTypeCode"/> is an editable reference (default from the cause) validated as an
/// active egreso; the authoritative concept is frozen from the cause default at Apply (aclaración №5).
/// </summary>
public sealed record DisciplinaryActionInput(
    Guid DisciplinaryActionTypePublicId,
    Guid DisciplinaryActionCausePublicId,
    DateOnly IncidentDate,
    string FactsDetail,
    bool HasPayrollDeduction,
    decimal? DeductionAmount,
    string? CurrencyCode,
    string? DeductionConceptTypeCode,
    DateOnly? SuspensionStartDate,
    DateOnly? SuspensionEndDate,
    Guid? AssignedPositionPublicId,
    string? Notes);

/// <summary>Canonical wire values of the disciplinary-action decision (RN-01).</summary>
public static class DisciplinaryActionDecisions
{
    public const string Apply = "APLICAR";
    public const string Reject = "RECHAZAR";

    public static bool IsValid(string? decision) =>
        string.Equals(decision, Apply, StringComparison.OrdinalIgnoreCase)
        || string.Equals(decision, Reject, StringComparison.OrdinalIgnoreCase);
}

public sealed record AddPersonnelFileDisciplinaryActionCommand(Guid PersonnelFileId, DisciplinaryActionInput Item)
    : ICommand<PersonnelFileDisciplinaryActionResponse>;

public sealed record UpdatePersonnelFileDisciplinaryActionCommand(
    Guid PersonnelFileId,
    Guid DisciplinaryActionPublicId,
    DisciplinaryActionInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileDisciplinaryActionResponse>;

/// <summary>The single decision (RN-01/RN-02): <c>APLICAR</c> or <c>RECHAZAR</c> (with a mandatory note).</summary>
public sealed record DecidePersonnelFileDisciplinaryActionCommand(
    Guid PersonnelFileId,
    Guid DisciplinaryActionPublicId,
    string Decision,
    string? Note,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileDisciplinaryActionResponse>;

/// <summary>Annulment (from EN_REVISION) or revocation (from APLICADA); the reason is mandatory (RN-07).</summary>
public sealed record AnnulPersonnelFileDisciplinaryActionCommand(
    Guid PersonnelFileId,
    Guid DisciplinaryActionPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileDisciplinaryActionResponse>;

public sealed record GetPersonnelFileDisciplinaryActionsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>>;

public sealed record GetPersonnelFileDisciplinaryActionByIdQuery(Guid PersonnelFileId, Guid DisciplinaryActionPublicId)
    : IQuery<PersonnelFileDisciplinaryActionResponse>;

// ── Validators ───────────────────────────────────────────────────────────────────────────────────

internal sealed class DisciplinaryActionInputValidator : AbstractValidator<DisciplinaryActionInput>
{
    public DisciplinaryActionInputValidator()
    {
        RuleFor(input => input.DisciplinaryActionTypePublicId).NotEmpty();
        RuleFor(input => input.DisciplinaryActionCausePublicId).NotEmpty();
        RuleFor(input => input.IncidentDate).NotEmpty();
        RuleFor(input => input.FactsDetail).NotEmpty().MaximumLength(2000);
        RuleFor(input => input.CurrencyCode).MaximumLength(10);
        RuleFor(input => input.DeductionConceptTypeCode).MaximumLength(80);
        RuleFor(input => input.Notes).MaximumLength(1000);
    }
}

internal sealed class AddPersonnelFileDisciplinaryActionCommandValidator : AbstractValidator<AddPersonnelFileDisciplinaryActionCommand>
{
    public AddPersonnelFileDisciplinaryActionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new DisciplinaryActionInputValidator());
    }
}

internal sealed class UpdatePersonnelFileDisciplinaryActionCommandValidator : AbstractValidator<UpdatePersonnelFileDisciplinaryActionCommand>
{
    public UpdatePersonnelFileDisciplinaryActionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DisciplinaryActionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new DisciplinaryActionInputValidator());
    }
}

internal sealed class DecidePersonnelFileDisciplinaryActionCommandValidator : AbstractValidator<DecidePersonnelFileDisciplinaryActionCommand>
{
    public DecidePersonnelFileDisciplinaryActionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DisciplinaryActionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Decision)
            .NotEmpty()
            .Must(DisciplinaryActionDecisions.IsValid)
            .WithMessage("Decision must be APLICAR or RECHAZAR.");
        RuleFor(command => command.Note).MaximumLength(1000);
    }
}

internal sealed class AnnulPersonnelFileDisciplinaryActionCommandValidator : AbstractValidator<AnnulPersonnelFileDisciplinaryActionCommand>
{
    public AnnulPersonnelFileDisciplinaryActionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DisciplinaryActionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(1000);
    }
}

internal sealed class GetPersonnelFileDisciplinaryActionsQueryValidator : AbstractValidator<GetPersonnelFileDisciplinaryActionsQuery>
{
    public GetPersonnelFileDisciplinaryActionsQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetPersonnelFileDisciplinaryActionByIdQueryValidator : AbstractValidator<GetPersonnelFileDisciplinaryActionByIdQuery>
{
    public GetPersonnelFileDisciplinaryActionByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.DisciplinaryActionPublicId).NotEmpty();
    }
}

/// <summary>
/// Dedicated errors for disciplinary actions ("amonestaciones"). Each NEW code requires an EN + ES resource
/// entry (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation lives in the validators (400).
/// The deduction-concept error (<c>DEDUCTION_CONCEPT_INVALID</c>, PR-1), the state-rule violation, the
/// decision-note / annulment-reason requirements (PR-3) and the retired-profile lock reuse existing codes.
/// </summary>
internal static class DisciplinaryActionErrors
{
    public static readonly Error TypeInvalid = new(
        "DISCIPLINARY_ACTION_TYPE_INVALID",
        "The disciplinary-action type does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error CauseInvalid = new(
        "DISCIPLINARY_ACTION_CAUSE_INVALID",
        "The disciplinary-action cause does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error IncidentDateInFuture = new(
        "DISCIPLINARY_ACTION_INCIDENT_DATE_IN_FUTURE",
        "The disciplinary-action incident date cannot be in the future.", ErrorType.UnprocessableEntity);

    public static readonly Error SuspensionNotAllowedForType = new(
        "SUSPENSION_NOT_ALLOWED_FOR_TYPE",
        "Suspension dates cannot travel on a disciplinary-action type that does not apply suspension.", ErrorType.UnprocessableEntity);

    public static readonly Error SuspensionDatesRequired = new(
        "SUSPENSION_DATES_REQUIRED",
        "A disciplinary-action type that applies suspension requires both suspension dates.", ErrorType.UnprocessableEntity);

    public static readonly Error SuspensionRangeInvalid = new(
        "SUSPENSION_RANGE_INVALID",
        "The suspension start date cannot be after the end date.", ErrorType.UnprocessableEntity);

    public static readonly Error SuspensionOverlap = new(
        "SUSPENSION_OVERLAP",
        "The suspension range overlaps another applied suspension of the employee.", ErrorType.UnprocessableEntity);

    public static readonly Error DeductionAmountRequired = new(
        "DEDUCTION_AMOUNT_REQUIRED",
        "A payroll deduction requires an amount greater than zero.", ErrorType.UnprocessableEntity);

    public static readonly Error DeductionConceptInvalid = new(
        "DEDUCTION_CONCEPT_INVALID",
        "The deduction concept could not be found, is inactive, or is not an expense (egreso) concept.", ErrorType.UnprocessableEntity);

    public static readonly Error SelfApprovalForbidden = new(
        "DISCIPLINARY_ACTION_SELF_APPROVAL_FORBIDDEN",
        "The subject employee or the registrar cannot decide or revoke the disciplinary action.", ErrorType.Forbidden);

    public static readonly Error StateRuleViolation = new(
        "PERSONNEL_TRANSACTION_STATE_RULE_VIOLATION",
        "The transaction is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    public static readonly Error DecisionNoteRequired = new(
        "DECISION_NOTE_REQUIRED",
        "A note is required to reject the transaction.", ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "ANNULMENT_REASON_REQUIRED",
        "A reason is required to annul or revoke the transaction.", ErrorType.UnprocessableEntity);
}
