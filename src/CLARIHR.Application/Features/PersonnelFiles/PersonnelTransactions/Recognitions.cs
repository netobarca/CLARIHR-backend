using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>
/// A recognition ("reconocimiento") with its one-decision lifecycle (REQ-003 D-02/D-07). Health-neutral merit
/// registered on the employee file that, once authorized, is applied with an automatic <c>RECONOCIMIENTO</c>
/// personnel-action entry. The self-service employee only ever sees their APLICADA recognitions (D-13).
/// </summary>
public sealed record PersonnelFileRecognitionResponse(
    Guid RecognitionPublicId,
    Guid RecognitionTypePublicId,
    string TypeNameSnapshot,
    DateOnly EventDate,
    string Detail,
    decimal? Amount,
    string? CurrencyCode,
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
    string? Notes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc)
{
    [JsonIgnore]
    public Guid Id => RecognitionPublicId;
}

/// <summary>Declarative fields for registering or editing a recognition. The type travels as a public id.</summary>
public sealed record RecognitionInput(
    Guid RecognitionTypePublicId,
    DateOnly EventDate,
    string Detail,
    decimal? Amount,
    string? CurrencyCode,
    Guid? AssignedPositionPublicId,
    string? Notes);

/// <summary>Canonical wire values of the recognition decision (RN-01).</summary>
public static class RecognitionDecisions
{
    public const string Apply = "APLICAR";
    public const string Reject = "RECHAZAR";

    public static bool IsValid(string? decision) =>
        string.Equals(decision, Apply, StringComparison.OrdinalIgnoreCase)
        || string.Equals(decision, Reject, StringComparison.OrdinalIgnoreCase);
}

public sealed record AddPersonnelFileRecognitionCommand(Guid PersonnelFileId, RecognitionInput Item)
    : ICommand<PersonnelFileRecognitionResponse>;

public sealed record UpdatePersonnelFileRecognitionCommand(
    Guid PersonnelFileId,
    Guid RecognitionPublicId,
    RecognitionInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRecognitionResponse>;

/// <summary>The single decision (RN-01/RN-02): <c>APLICAR</c> or <c>RECHAZAR</c> (with a mandatory note).</summary>
public sealed record DecidePersonnelFileRecognitionCommand(
    Guid PersonnelFileId,
    Guid RecognitionPublicId,
    string Decision,
    string? Note,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRecognitionResponse>;

/// <summary>Annulment (from EN_REVISION) or revocation (from APLICADA); the reason is mandatory (RN-07).</summary>
public sealed record AnnulPersonnelFileRecognitionCommand(
    Guid PersonnelFileId,
    Guid RecognitionPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRecognitionResponse>;

public sealed record GetPersonnelFileRecognitionsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileRecognitionResponse>>;

public sealed record GetPersonnelFileRecognitionByIdQuery(Guid PersonnelFileId, Guid RecognitionPublicId)
    : IQuery<PersonnelFileRecognitionResponse>;

// ── Validators ───────────────────────────────────────────────────────────────────────────────────

internal sealed class RecognitionInputValidator : AbstractValidator<RecognitionInput>
{
    public RecognitionInputValidator()
    {
        RuleFor(input => input.RecognitionTypePublicId).NotEmpty();
        RuleFor(input => input.EventDate).NotEmpty();
        RuleFor(input => input.Detail).NotEmpty().MaximumLength(1000);
        RuleFor(input => input.CurrencyCode).MaximumLength(10);
        RuleFor(input => input.Notes).MaximumLength(1000);
    }
}

internal sealed class AddPersonnelFileRecognitionCommandValidator : AbstractValidator<AddPersonnelFileRecognitionCommand>
{
    public AddPersonnelFileRecognitionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new RecognitionInputValidator());
    }
}

internal sealed class UpdatePersonnelFileRecognitionCommandValidator : AbstractValidator<UpdatePersonnelFileRecognitionCommand>
{
    public UpdatePersonnelFileRecognitionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecognitionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new RecognitionInputValidator());
    }
}

internal sealed class DecidePersonnelFileRecognitionCommandValidator : AbstractValidator<DecidePersonnelFileRecognitionCommand>
{
    public DecidePersonnelFileRecognitionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecognitionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Decision)
            .NotEmpty()
            .Must(RecognitionDecisions.IsValid)
            .WithMessage("Decision must be APLICAR or RECHAZAR.");
        RuleFor(command => command.Note).MaximumLength(1000);
    }
}

internal sealed class AnnulPersonnelFileRecognitionCommandValidator : AbstractValidator<AnnulPersonnelFileRecognitionCommand>
{
    public AnnulPersonnelFileRecognitionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecognitionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(1000);
    }
}

internal sealed class GetPersonnelFileRecognitionsQueryValidator : AbstractValidator<GetPersonnelFileRecognitionsQuery>
{
    public GetPersonnelFileRecognitionsQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetPersonnelFileRecognitionByIdQueryValidator : AbstractValidator<GetPersonnelFileRecognitionByIdQuery>
{
    public GetPersonnelFileRecognitionByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecognitionPublicId).NotEmpty();
    }
}

/// <summary>
/// Dedicated errors for recognitions ("reconocimientos"). Each code requires an EN + ES resource entry
/// (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation lives in the validators (400).
/// The state-rule violation and the retired-profile lock are shared with the transactions module / reused from
/// <c>PersonnelFileErrors</c>.
/// </summary>
internal static class RecognitionErrors
{
    public static readonly Error TypeInvalid = new(
        "RECOGNITION_TYPE_INVALID",
        "The recognition type does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error EventDateInFuture = new(
        "RECOGNITION_EVENT_DATE_IN_FUTURE",
        "The recognition event date cannot be in the future.", ErrorType.UnprocessableEntity);

    public static readonly Error AmountInvalid = new(
        "RECOGNITION_AMOUNT_INVALID",
        "The recognition amount must be greater than zero and carry a currency when it travels.", ErrorType.UnprocessableEntity);

    public static readonly Error SelfApprovalForbidden = new(
        "RECOGNITION_SELF_APPROVAL_FORBIDDEN",
        "The subject employee or the registrar cannot decide or revoke the recognition.", ErrorType.Forbidden);

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
