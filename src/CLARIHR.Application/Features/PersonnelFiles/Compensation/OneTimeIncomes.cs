using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// A one-time income of a personnel file ("ingreso eventual", REQ-006): the header + the settled compensation
/// concept (with its name snapshot), the value (fixed or computed — quantity × unit value × multiplier, or a
/// percentage over a base — with the resolved <see cref="Amount"/>), the mandatory plaza + cost center (P-15,
/// with the cost-center name snapshot), the requester trío (file + name snapshot), the payroll destination and
/// the EN_REVISION → AUTORIZADO → APLICADO lifecycle with its rejection/annulment branches. User ids are nullable
/// (a non-Guid principal maps to null — lesson REQ-003). The applications history + projection are PR-4/PR-5.
/// </summary>
public sealed record OneTimeIncomeResponse(
    Guid OneTimeIncomePublicId,
    DateOnly IncomeDate,
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
    Guid CostCenterPublicId,
    string CostCenterNameSnapshot,
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
    public Guid Id => OneTimeIncomePublicId;
}

/// <summary>
/// Business fields for registering or editing a one-time income. The status/decision/annulment fields are NOT set
/// here — they are driven by the dedicated lifecycle actions. The value coherence is validated + the amount is
/// resolved through the pure <c>OneTimeIncomeRules</c> in the handler (422 when incoherent).
/// <see cref="AssignedPositionPublicId"/> is optional: when omitted the employee's principal plaza is resolved and
/// the cost center is DERIVED from it (P-15). <see cref="CurrencyCode"/> is optional: it defaults to the company
/// preference currency when omitted. <see cref="RequesterFilePublicId"/> (the trío) is mandatory.
/// </summary>
public sealed record OneTimeIncomeInput(
    DateOnly IncomeDate,
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

/// <summary>Payroll destination fields for the re-imputation ("enviar a otro periodo", RF-005 — only AUTORIZADO).</summary>
public sealed record OneTimeIncomePeriodInput(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>
/// Result of resolving the plaza + cost center of a one-time income (P-15). <see cref="Found"/> is false when no
/// assignment matches; <see cref="CostCenterPublicId"/> is null when the resolved plaza has no cost center
/// (→ ONE_TIME_INCOME_COST_CENTER_MISSING). Returned by the repository plaza resolver.
/// </summary>
public sealed record OneTimeIncomePlazaResolution(
    bool Found,
    Guid AssignedPositionPublicId,
    Guid? CostCenterPublicId,
    string? CostCenterName)
{
    public static readonly OneTimeIncomePlazaResolution NotFound = new(false, Guid.Empty, null, null);
}

/// <summary>
/// Requester lookup for the trío (№10) + the TRIPLE anti-self pata (c) (№6): the display name, activity and
/// linked login of a personnel file of the company.
/// </summary>
public sealed record OneTimeIncomeRequesterLookup(
    Guid FilePublicId,
    string FullName,
    bool IsActive,
    Guid? LinkedUserPublicId);

/// <summary>
/// Compensation-concept lookup for a one-time income (D-03): the name snapshot + activity + nature + base-salary
/// flag, so the handler can run <c>OneTimeIncomeRules.ValidateConcept</c> (Nature = Ingreso, not base salary).
/// </summary>
public sealed record OneTimeIncomeConceptLookup(
    string Name,
    bool IsActive,
    CompensationNature Nature,
    bool IsBaseSalary);

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

public sealed record AddPersonnelFileOneTimeIncomeCommand(
    Guid PersonnelFileId,
    OneTimeIncomeInput Item)
    : ICommand<OneTimeIncomeResponse>;

public sealed record UpdatePersonnelFileOneTimeIncomeCommand(
    Guid PersonnelFileId,
    Guid OneTimeIncomePublicId,
    OneTimeIncomeInput Item,
    Guid ConcurrencyToken)
    : ICommand<OneTimeIncomeResponse>;

public sealed record DeletePersonnelFileOneTimeIncomeCommand(
    Guid PersonnelFileId,
    Guid OneTimeIncomePublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

/// <summary>Annul (retiro) an EN_REVISION income (→ ANULADO, HR/Manage); the reason is mandatory.</summary>
public sealed record AnnulPersonnelFileOneTimeIncomeCommand(
    Guid PersonnelFileId,
    Guid OneTimeIncomePublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OneTimeIncomeResponse>;

/// <summary>Re-target ("enviar a otro periodo", RF-005) the payroll destination of an AUTORIZADO income (HR/Manage).</summary>
public sealed record RetargetPersonnelFileOneTimeIncomePeriodCommand(
    Guid PersonnelFileId,
    Guid OneTimeIncomePublicId,
    OneTimeIncomePeriodInput Period,
    Guid ConcurrencyToken)
    : ICommand<OneTimeIncomeResponse>;

/// <summary>Authorizer resolution of an EN_REVISION income: <c>TargetStatusCode</c> = AUTORIZADO (authorize) or
/// RECHAZADO (reject — note mandatory). TRIPLE anti-self (subject / registrar / requester) — aclaración №6.</summary>
public sealed record ResolvePersonnelFileOneTimeIncomeCommand(
    Guid PersonnelFileId,
    Guid OneTimeIncomePublicId,
    string TargetStatusCode,
    string? Note,
    Guid ConcurrencyToken)
    : ICommand<OneTimeIncomeResponse>;

/// <summary>Authorizer revocation of an AUTORIZADO income (→ ANULADO); the reason is mandatory. TRIPLE anti-self.</summary>
public sealed record RevokePersonnelFileOneTimeIncomeCommand(
    Guid PersonnelFileId,
    Guid OneTimeIncomePublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OneTimeIncomeResponse>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

public sealed record GetPersonnelFileOneTimeIncomesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<OneTimeIncomeResponse>>;

public sealed record GetPersonnelFileOneTimeIncomeByIdQuery(Guid PersonnelFileId, Guid OneTimeIncomePublicId)
    : IQuery<OneTimeIncomeResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class OneTimeIncomeInputValidator : AbstractValidator<OneTimeIncomeInput>
{
    public OneTimeIncomeInputValidator()
    {
        RuleFor(input => input.IncomeDate).NotEmpty();
        RuleFor(input => input.Reference).MaximumLength(PersonnelFileOneTimeIncome.MaxReferenceLength);
        RuleFor(input => input.ConceptTypeCode).NotEmpty().MaximumLength(PersonnelFileOneTimeIncome.MaxConceptTypeCodeLength);
        RuleFor(input => input.Observations).MaximumLength(PersonnelFileOneTimeIncome.MaxObservationsLength);

        // The calculation method + components are shaped by the pure OneTimeIncomeRules in the handler (422 with
        // the granular value code); here only the bounds/positivity of the supplied fields are validated (400).
        RuleFor(input => input.CalculationMethod).MaximumLength(PersonnelFileOneTimeIncome.MaxCalculationMethodLength);
        RuleFor(input => input.Quantity).GreaterThan(0m).When(input => input.Quantity.HasValue);
        RuleFor(input => input.UnitValue).GreaterThan(0m).When(input => input.UnitValue.HasValue);
        RuleFor(input => input.Multiplier).GreaterThan(0m).When(input => input.Multiplier.HasValue);
        RuleFor(input => input.Percentage).GreaterThan(0m).When(input => input.Percentage.HasValue);
        RuleFor(input => input.BaseAmount).GreaterThan(0m).When(input => input.BaseAmount.HasValue);
        RuleFor(input => input.Amount).GreaterThan(0m).When(input => input.Amount.HasValue);

        RuleFor(input => input.CurrencyCode)
            .Length(PersonnelFileOneTimeIncome.MaxCurrencyCodeLength)
            .When(input => !string.IsNullOrWhiteSpace(input.CurrencyCode));

        RuleFor(input => input.RequesterFilePublicId).NotEmpty();
        RuleFor(input => input.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOneTimeIncome.MaxPayrollTypeCodeLength);
        RuleFor(input => input.PayrollPeriodLabel).NotEmpty().MaximumLength(PersonnelFileOneTimeIncome.MaxPayrollPeriodLabelLength);
    }
}

internal sealed class OneTimeIncomePeriodInputValidator : AbstractValidator<OneTimeIncomePeriodInput>
{
    public OneTimeIncomePeriodInputValidator()
    {
        RuleFor(input => input.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOneTimeIncome.MaxPayrollTypeCodeLength);
        RuleFor(input => input.PayrollPeriodLabel).NotEmpty().MaximumLength(PersonnelFileOneTimeIncome.MaxPayrollPeriodLabelLength);
    }
}

internal sealed class AddPersonnelFileOneTimeIncomeCommandValidator : AbstractValidator<AddPersonnelFileOneTimeIncomeCommand>
{
    public AddPersonnelFileOneTimeIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new OneTimeIncomeInputValidator());
    }
}

internal sealed class UpdatePersonnelFileOneTimeIncomeCommandValidator : AbstractValidator<UpdatePersonnelFileOneTimeIncomeCommand>
{
    public UpdatePersonnelFileOneTimeIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new OneTimeIncomeInputValidator());
    }
}

internal sealed class DeletePersonnelFileOneTimeIncomeCommandValidator : AbstractValidator<DeletePersonnelFileOneTimeIncomeCommand>
{
    public DeletePersonnelFileOneTimeIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AnnulPersonnelFileOneTimeIncomeCommandValidator : AbstractValidator<AnnulPersonnelFileOneTimeIncomeCommand>
{
    public AnnulPersonnelFileOneTimeIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOneTimeIncome.MaxAnnulmentReasonLength);
    }
}

internal sealed class RetargetPersonnelFileOneTimeIncomePeriodCommandValidator : AbstractValidator<RetargetPersonnelFileOneTimeIncomePeriodCommand>
{
    public RetargetPersonnelFileOneTimeIncomePeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Period).NotNull().SetValidator(new OneTimeIncomePeriodInputValidator());
    }
}

internal sealed class ResolvePersonnelFileOneTimeIncomeCommandValidator : AbstractValidator<ResolvePersonnelFileOneTimeIncomeCommand>
{
    public ResolvePersonnelFileOneTimeIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.TargetStatusCode).NotEmpty().MaximumLength(PersonnelFileOneTimeIncome.MaxStatusCodeLength);
        RuleFor(command => command.Note).MaximumLength(PersonnelFileOneTimeIncome.MaxDecisionNoteLength);
    }
}

internal sealed class RevokePersonnelFileOneTimeIncomeCommandValidator : AbstractValidator<RevokePersonnelFileOneTimeIncomeCommand>
{
    public RevokePersonnelFileOneTimeIncomeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OneTimeIncomePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOneTimeIncome.MaxAnnulmentReasonLength);
    }
}

internal sealed class GetPersonnelFileOneTimeIncomesQueryValidator : AbstractValidator<GetPersonnelFileOneTimeIncomesQuery>
{
    public GetPersonnelFileOneTimeIncomesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileOneTimeIncomeByIdQueryValidator : AbstractValidator<GetPersonnelFileOneTimeIncomeByIdQuery>
{
    public GetPersonnelFileOneTimeIncomeByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OneTimeIncomePublicId).NotEmpty();
    }
}
