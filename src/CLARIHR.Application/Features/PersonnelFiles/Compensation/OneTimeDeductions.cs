using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// A one-off deduction of a personnel file ("descuento eventual", REQ-009): a compensation concept the company
/// charges the employee a single time. Mirrors the one-time INCOME but on the deduction side, with two deltas:
/// there is NO cost center (P-08 — only the plaza) and the concept must be an ACTIVE, NON-STATUTORY
/// <c>Egreso</c> concept. The value is either fixed or DERIVED from its persisted components (the server owns the
/// amount). User ids are nullable (a non-Guid principal maps to null).
/// </summary>
public sealed record OneTimeDeductionResponse(
    Guid OneTimeDeductionPublicId,
    DateOnly DeductionDate,
    string? Reference,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    string? Observations,
    bool IsFixedValue,
    string? CalculationMethod,
    decimal? Quantity,
    decimal? UnitValue,
    decimal? Multiplier,
    decimal? Percentage,
    decimal? BaseAmount,
    decimal Amount,
    string CurrencyCode,
    Guid AssignedPositionPublicId,
    Guid RequesterFilePublicId,
    string RequesterNameSnapshot,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate,
    string StatusCode,
    Guid? RequestedByUserId,
    Guid? DecidedByUserId,
    DateTime? DecidedUtc,
    string? DecisionNote,
    Guid? AnnulledByUserId,
    DateTime? AnnulledUtc,
    string? AnnulmentReason,
    Guid? AppliedBySettlementPublicId,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => OneTimeDeductionPublicId;
}

/// <summary>
/// Business fields for registering or editing a one-time deduction. <see cref="AssignedPositionPublicId"/> is
/// optional (the principal plaza is resolved when omitted; there is no cost center). <see cref="Amount"/> is
/// OPTIONAL when the value is computed: the server DERIVES it from the components, and a supplied amount that
/// does not match them is rejected with the expected figure (422).
/// </summary>
public sealed record OneTimeDeductionInput(
    DateOnly DeductionDate,
    string? Reference,
    string ConceptTypeCode,
    string? Observations,
    bool IsFixedValue,
    string? CalculationMethod,
    decimal? Quantity,
    decimal? UnitValue,
    decimal? Multiplier,
    decimal? Percentage,
    decimal? BaseAmount,
    decimal? Amount,
    string? CurrencyCode,
    Guid? AssignedPositionPublicId,
    Guid RequesterFilePublicId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Payroll destination fields for the re-imputation ("enviar a otro periodo" — only while AUTORIZADO).</summary>
public sealed record OneTimeDeductionPeriodInput(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>
/// Result of resolving the plaza of a one-time deduction. <see cref="Found"/> is false when no assignment matches.
/// Unlike the one-time income there is NO cost center (P-08).
/// </summary>
public sealed record OneTimeDeductionPlazaResolution(bool Found, Guid AssignedPositionPublicId)
{
    public static readonly OneTimeDeductionPlazaResolution NotFound = new(false, Guid.Empty);
}

/// <summary>
/// The requester of a one-time deduction (the trío): the personnel file, its name (snapshotted onto the record)
/// and its linked login — which is the THIRD leg of the anti-self triple (the requester cannot decide).
/// </summary>
public sealed record OneTimeDeductionRequesterLookup(
    Guid FilePublicId,
    string FullName,
    Guid? LinkedUserPublicId);

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

public sealed record AddPersonnelFileOneTimeDeductionCommand(
    Guid PersonnelFileId,
    OneTimeDeductionInput Item)
    : ICommand<OneTimeDeductionResponse>;

public sealed record UpdatePersonnelFileOneTimeDeductionCommand(
    Guid PersonnelFileId,
    Guid OneTimeDeductionPublicId,
    OneTimeDeductionInput Item,
    Guid ConcurrencyToken)
    : ICommand<OneTimeDeductionResponse>;

public sealed record DeletePersonnelFileOneTimeDeductionCommand(
    Guid PersonnelFileId,
    Guid OneTimeDeductionPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

/// <summary>Annul an EN_REVISION deduction (→ ANULADO, HR/Manage); the reason is mandatory.</summary>
public sealed record AnnulPersonnelFileOneTimeDeductionCommand(
    Guid PersonnelFileId,
    Guid OneTimeDeductionPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OneTimeDeductionResponse>;

/// <summary>Re-target the payroll destination of an AUTORIZADO deduction ("enviar a otro periodo").</summary>
public sealed record RetargetPersonnelFileOneTimeDeductionPeriodCommand(
    Guid PersonnelFileId,
    Guid OneTimeDeductionPublicId,
    OneTimeDeductionPeriodInput Item,
    Guid ConcurrencyToken)
    : ICommand<OneTimeDeductionResponse>;

/// <summary>Authorizer resolution of an EN_REVISION deduction: <c>TargetStatusCode</c> = AUTORIZADO or RECHAZADO
/// (note mandatory). Anti-self TRIPLE (subject / requester / registrar).</summary>
public sealed record ResolvePersonnelFileOneTimeDeductionCommand(
    Guid PersonnelFileId,
    Guid OneTimeDeductionPublicId,
    string TargetStatusCode,
    string? Note,
    Guid ConcurrencyToken)
    : ICommand<OneTimeDeductionResponse>;

/// <summary>Authorizer revocation of an AUTORIZADO deduction (→ ANULADO); the reason is mandatory. Anti-self TRIPLE.</summary>
public sealed record RevokePersonnelFileOneTimeDeductionCommand(
    Guid PersonnelFileId,
    Guid OneTimeDeductionPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OneTimeDeductionResponse>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

public sealed record GetPersonnelFileOneTimeDeductionsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<OneTimeDeductionResponse>>;

public sealed record GetPersonnelFileOneTimeDeductionByIdQuery(Guid PersonnelFileId, Guid OneTimeDeductionPublicId)
    : IQuery<OneTimeDeductionResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class OneTimeDeductionInputValidator : AbstractValidator<OneTimeDeductionInput>
{
    public OneTimeDeductionInputValidator()
    {
        RuleFor(input => input.DeductionDate).NotEmpty();
        RuleFor(input => input.Reference).MaximumLength(PersonnelFileOneTimeDeduction.MaxReferenceLength);
        RuleFor(input => input.ConceptTypeCode).NotEmpty().MaximumLength(PersonnelFileOneTimeDeduction.MaxConceptTypeCodeLength);
        RuleFor(input => input.Observations).MaximumLength(PersonnelFileOneTimeDeduction.MaxObservationsLength);
        RuleFor(input => input.CurrencyCode)
            .Length(PersonnelFileOneTimeDeduction.MaxCurrencyCodeLength)
            .When(input => !string.IsNullOrWhiteSpace(input.CurrencyCode));

        RuleFor(input => input.CalculationMethod).MaximumLength(PersonnelFileOneTimeDeduction.MaxCalculationMethodLength);

        // Positive components when supplied; the coherence with the method (and the amount cross-check) is the
        // handler's job through the pure OneTimeDeductionRules (422 with the granular code + expected amount).
        RuleFor(input => input.Quantity).GreaterThan(0m).When(input => input.Quantity.HasValue);
        RuleFor(input => input.UnitValue).GreaterThan(0m).When(input => input.UnitValue.HasValue);
        RuleFor(input => input.Multiplier).GreaterThan(0m).When(input => input.Multiplier.HasValue);
        RuleFor(input => input.Percentage).GreaterThan(0m).When(input => input.Percentage.HasValue);
        RuleFor(input => input.BaseAmount).GreaterThan(0m).When(input => input.BaseAmount.HasValue);
        RuleFor(input => input.Amount).GreaterThan(0m).When(input => input.Amount.HasValue);

        // A FIXED value must carry its amount; a computed one may omit it (the server derives it).
        RuleFor(input => input.Amount)
            .NotNull()
            .When(input => input.IsFixedValue)
            .WithMessage("A fixed-value deduction requires an amount.");

        RuleFor(input => input.RequesterFilePublicId).NotEmpty();
        RuleFor(input => input.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOneTimeDeduction.MaxPayrollTypeCodeLength);
        RuleFor(input => input.PayrollPeriodLabel).NotEmpty().MaximumLength(PersonnelFileOneTimeDeduction.MaxPayrollPeriodLabelLength);
    }
}

internal sealed class OneTimeDeductionPeriodInputValidator : AbstractValidator<OneTimeDeductionPeriodInput>
{
    public OneTimeDeductionPeriodInputValidator()
    {
        RuleFor(input => input.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOneTimeDeduction.MaxPayrollTypeCodeLength);
        RuleFor(input => input.PayrollPeriodLabel).NotEmpty().MaximumLength(PersonnelFileOneTimeDeduction.MaxPayrollPeriodLabelLength);
    }
}

internal sealed class AddPersonnelFileOneTimeDeductionCommandValidator : AbstractValidator<AddPersonnelFileOneTimeDeductionCommand>
{
    public AddPersonnelFileOneTimeDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new OneTimeDeductionInputValidator());
    }
}

internal sealed class UpdatePersonnelFileOneTimeDeductionCommandValidator : AbstractValidator<UpdatePersonnelFileOneTimeDeductionCommand>
{
    public UpdatePersonnelFileOneTimeDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new OneTimeDeductionInputValidator());
    }
}

internal sealed class DeletePersonnelFileOneTimeDeductionCommandValidator : AbstractValidator<DeletePersonnelFileOneTimeDeductionCommand>
{
    public DeletePersonnelFileOneTimeDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AnnulPersonnelFileOneTimeDeductionCommandValidator : AbstractValidator<AnnulPersonnelFileOneTimeDeductionCommand>
{
    public AnnulPersonnelFileOneTimeDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOneTimeDeduction.MaxAnnulmentReasonLength);
    }
}

internal sealed class RetargetPersonnelFileOneTimeDeductionPeriodCommandValidator
    : AbstractValidator<RetargetPersonnelFileOneTimeDeductionPeriodCommand>
{
    public RetargetPersonnelFileOneTimeDeductionPeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new OneTimeDeductionPeriodInputValidator());
    }
}

internal sealed class ResolvePersonnelFileOneTimeDeductionCommandValidator : AbstractValidator<ResolvePersonnelFileOneTimeDeductionCommand>
{
    public ResolvePersonnelFileOneTimeDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.TargetStatusCode).NotEmpty().MaximumLength(PersonnelFileOneTimeDeduction.MaxStatusCodeLength);
        RuleFor(command => command.Note).MaximumLength(PersonnelFileOneTimeDeduction.MaxDecisionNoteLength);
    }
}

internal sealed class RevokePersonnelFileOneTimeDeductionCommandValidator : AbstractValidator<RevokePersonnelFileOneTimeDeductionCommand>
{
    public RevokePersonnelFileOneTimeDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOneTimeDeduction.MaxAnnulmentReasonLength);
    }
}

internal sealed class GetPersonnelFileOneTimeDeductionsQueryValidator : AbstractValidator<GetPersonnelFileOneTimeDeductionsQuery>
{
    public GetPersonnelFileOneTimeDeductionsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileOneTimeDeductionByIdQueryValidator : AbstractValidator<GetPersonnelFileOneTimeDeductionByIdQuery>
{
    public GetPersonnelFileOneTimeDeductionByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OneTimeDeductionPublicId).NotEmpty();
    }
}
