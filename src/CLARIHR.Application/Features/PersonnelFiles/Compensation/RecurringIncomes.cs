using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// A recurring-income agreement of a personnel file ("ingreso cíclico", REQ-005): the header + the settled
/// compensation concept (with its name snapshot), the mandatory plaza + cost center (P-15, with the cost-center
/// name snapshot), the installment plan and the EN_REVISION → VIGENTE → (SUSPENDIDO) → FINALIZADO lifecycle with
/// its rejection/annulment branches. User ids are nullable (a non-Guid principal maps to null — lesson REQ-003).
/// The applied installments and the projection are PR-4.
/// </summary>
public sealed record RecurringIncomeResponse(
    Guid RecurringIncomePublicId,
    DateOnly RegistrationDate,
    string? Reference,
    string RecurringIncomeTypeCode,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    string? Observations,
    Guid AssignedPositionPublicId,
    Guid CostCenterPublicId,
    string CostCenterNameSnapshot,
    DateOnly InstallmentStartDate,
    string CurrencyCode,
    string PayrollTypeCode,
    string InstallmentFrequencyCode,
    bool IsIndefinite,
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    string SettlementActionCode,
    string StatusCode,
    Guid? RegisteredByUserId,
    Guid? DecidedByUserId,
    DateTime? DecidedUtc,
    string? DecisionNote,
    DateTime? SuspendedUtc,
    string? SuspensionNote,
    DateTime? ClosedUtc,
    string? ClosureReason,
    Guid? ClosedByUserId,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => RecurringIncomePublicId;
}

/// <summary>
/// Business fields for registering or editing a recurring income. The status/decision/suspension/closure fields
/// are NOT set here — they are driven by the dedicated lifecycle actions. The plan is normalized through the pure
/// <c>RecurringIncomeRules</c> in the handler (422 when incoherent). <see cref="AssignedPositionPublicId"/> is
/// optional: when omitted the employee's principal plaza is resolved; the cost center is DERIVED from the plaza
/// (P-15).
/// </summary>
public sealed record RecurringIncomeInput(
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

/// <summary>
/// Result of resolving the plaza + cost center of a recurring income (P-15). <see cref="Found"/> is false when no
/// assignment matches; <see cref="CostCenterPublicId"/> is null when the resolved plaza has no cost center
/// (→ RECURRING_INCOME_COST_CENTER_MISSING). Returned by the repository plaza resolver.
/// </summary>
public sealed record RecurringIncomePlazaResolution(
    bool Found,
    Guid AssignedPositionPublicId,
    Guid? CostCenterPublicId,
    string? CostCenterName)
{
    public static readonly RecurringIncomePlazaResolution NotFound = new(false, Guid.Empty, null, null);
}

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

public sealed record AddPersonnelFileRecurringIncomeCommand(
    Guid PersonnelFileId,
    RecurringIncomeInput Item)
    : ICommand<RecurringIncomeResponse>;

public sealed record UpdatePersonnelFileRecurringIncomeCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    RecurringIncomeInput Item,
    Guid ConcurrencyToken)
    : ICommand<RecurringIncomeResponse>;

public sealed record DeletePersonnelFileRecurringIncomeCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

/// <summary>Suspend a VIGENTE income (<c>Suspend</c> = true, note optional) or resume a SUSPENDIDO one (false).</summary>
public sealed record SetPersonnelFileRecurringIncomeSuspensionCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    bool Suspend,
    string? Note,
    Guid ConcurrencyToken)
    : ICommand<RecurringIncomeResponse>;

/// <summary>Close an INDEFINITE VIGENTE income by hand (→ FINALIZADO); the reason is mandatory (P-06).</summary>
public sealed record ClosePersonnelFileRecurringIncomeCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<RecurringIncomeResponse>;

/// <summary>Annul an EN_REVISION income (→ ANULADO, HR/Manage); the reason is mandatory.</summary>
public sealed record AnnulPersonnelFileRecurringIncomeCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<RecurringIncomeResponse>;

/// <summary>Authorizer resolution of an EN_REVISION income: <c>TargetStatusCode</c> = VIGENTE (authorize) or
/// RECHAZADO (reject — note mandatory). Double anti-self (subject / registrar) — aclaración №6.</summary>
public sealed record ResolvePersonnelFileRecurringIncomeCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    string TargetStatusCode,
    string? Note,
    Guid ConcurrencyToken)
    : ICommand<RecurringIncomeResponse>;

/// <summary>Authorizer revocation of a VIGENTE income (→ ANULADO); the reason is mandatory. Double anti-self.</summary>
public sealed record RevokePersonnelFileRecurringIncomeCommand(
    Guid PersonnelFileId,
    Guid RecurringIncomePublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<RecurringIncomeResponse>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

public sealed record GetPersonnelFileRecurringIncomesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<RecurringIncomeResponse>>;

public sealed record GetPersonnelFileRecurringIncomeByIdQuery(Guid PersonnelFileId, Guid RecurringIncomePublicId)
    : IQuery<RecurringIncomeResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class RecurringIncomeInputValidator : AbstractValidator<RecurringIncomeInput>
{
    public RecurringIncomeInputValidator()
    {
        RuleFor(input => input.RegistrationDate).NotEmpty();
        RuleFor(input => input.Reference).MaximumLength(PersonnelFileRecurringIncome.MaxReferenceLength);
        RuleFor(input => input.RecurringIncomeTypeCode).NotEmpty().MaximumLength(PersonnelFileRecurringIncome.MaxRecurringIncomeTypeCodeLength);
        RuleFor(input => input.ConceptTypeCode).NotEmpty().MaximumLength(PersonnelFileRecurringIncome.MaxConceptTypeCodeLength);
        RuleFor(input => input.Observations).MaximumLength(PersonnelFileRecurringIncome.MaxObservationsLength);
        RuleFor(input => input.InstallmentStartDate).NotEmpty();
        RuleFor(input => input.CurrencyCode).NotEmpty().Length(PersonnelFileRecurringIncome.MaxCurrencyCodeLength);
        RuleFor(input => input.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileRecurringIncome.MaxPayrollTypeCodeLength);
        RuleFor(input => input.InstallmentFrequencyCode).NotEmpty().MaximumLength(PersonnelFileRecurringIncome.MaxInstallmentFrequencyCodeLength);

        // The installment value is always required and positive; count/total (finite plan) — when supplied — must
        // be positive. The plan coherence (indefinite ⇒ neither; finite ⇒ one derives the other) is normalized in
        // the handler through RecurringIncomeRules (422 with the granular plan code).
        RuleFor(input => input.InstallmentValue)
            .GreaterThan(0m)
            .WithMessage("InstallmentValue must be greater than zero.");
        RuleFor(input => input.InstallmentCount)
            .GreaterThan(0)
            .When(input => input.InstallmentCount.HasValue)
            .WithMessage("InstallmentCount must be greater than zero.");
        RuleFor(input => input.TotalAmount)
            .GreaterThan(0m)
            .When(input => input.TotalAmount.HasValue)
            .WithMessage("TotalAmount must be greater than zero.");

        RuleFor(input => input.SettlementActionCode)
            .NotEmpty()
            .MaximumLength(PersonnelFileRecurringIncome.MaxSettlementActionCodeLength)
            .Must(code => code is RecurringIncomeSettlementActions.PagarSaldo or RecurringIncomeSettlementActions.Cancelar)
            .WithMessage("SettlementActionCode must be PAGAR_SALDO or CANCELAR.");
    }
}

internal sealed class AddPersonnelFileRecurringIncomeCommandValidator : AbstractValidator<AddPersonnelFileRecurringIncomeCommand>
{
    public AddPersonnelFileRecurringIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new RecurringIncomeInputValidator());
    }
}

internal sealed class UpdatePersonnelFileRecurringIncomeCommandValidator : AbstractValidator<UpdatePersonnelFileRecurringIncomeCommand>
{
    public UpdatePersonnelFileRecurringIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new RecurringIncomeInputValidator());
    }
}

internal sealed class DeletePersonnelFileRecurringIncomeCommandValidator : AbstractValidator<DeletePersonnelFileRecurringIncomeCommand>
{
    public DeletePersonnelFileRecurringIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SetPersonnelFileRecurringIncomeSuspensionCommandValidator : AbstractValidator<SetPersonnelFileRecurringIncomeSuspensionCommand>
{
    public SetPersonnelFileRecurringIncomeSuspensionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Note).MaximumLength(PersonnelFileRecurringIncome.MaxSuspensionNoteLength);
    }
}

internal sealed class ClosePersonnelFileRecurringIncomeCommandValidator : AbstractValidator<ClosePersonnelFileRecurringIncomeCommand>
{
    public ClosePersonnelFileRecurringIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileRecurringIncome.MaxClosureReasonLength);
    }
}

internal sealed class AnnulPersonnelFileRecurringIncomeCommandValidator : AbstractValidator<AnnulPersonnelFileRecurringIncomeCommand>
{
    public AnnulPersonnelFileRecurringIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileRecurringIncome.MaxClosureReasonLength);
    }
}

internal sealed class ResolvePersonnelFileRecurringIncomeCommandValidator : AbstractValidator<ResolvePersonnelFileRecurringIncomeCommand>
{
    public ResolvePersonnelFileRecurringIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.TargetStatusCode).NotEmpty().MaximumLength(PersonnelFileRecurringIncome.MaxStatusCodeLength);
        RuleFor(command => command.Note).MaximumLength(PersonnelFileRecurringIncome.MaxDecisionNoteLength);
    }
}

internal sealed class RevokePersonnelFileRecurringIncomeCommandValidator : AbstractValidator<RevokePersonnelFileRecurringIncomeCommand>
{
    public RevokePersonnelFileRecurringIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileRecurringIncome.MaxClosureReasonLength);
    }
}

internal sealed class GetPersonnelFileRecurringIncomesQueryValidator : AbstractValidator<GetPersonnelFileRecurringIncomesQuery>
{
    public GetPersonnelFileRecurringIncomesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileRecurringIncomeByIdQueryValidator : AbstractValidator<GetPersonnelFileRecurringIncomeByIdQuery>
{
    public GetPersonnelFileRecurringIncomeByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecurringIncomePublicId).NotEmpty();
    }
}
