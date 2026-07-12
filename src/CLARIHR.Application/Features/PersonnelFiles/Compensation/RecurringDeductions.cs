using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>One segment of the plan definition on the wire (№12): installments <c>From</c>..<c>To</c> are worth
/// <c>InstallmentValue</c> each. <c>ToInstallment</c> is null only on the single open segment of an indefinite
/// plan; a compound-interest credit sends NO segments (its plan is derived).</summary>
public sealed record RecurringDeductionSegmentInput(
    int FromInstallment,
    int? ToInstallment,
    decimal InstallmentValue);

/// <summary>One segment of the plan as returned (carries its public id so the FE can key the rows).</summary>
public sealed record RecurringDeductionSegmentResponse(
    Guid SegmentPublicId,
    int FromInstallment,
    int? ToInstallment,
    decimal InstallmentValue);

/// <summary>
/// A recurring-deduction agreement of a personnel file ("descuento cíclico", REQ-008): the header (the credit
/// reference, the deduction type, the settled compensation concept with its name snapshot and the financial
/// institution), the plaza (D-13 — no cost center, P-08), the installment plan (start date, exception months,
/// frequencies, settlement action) expressed either as <see cref="Segments"/> or as a compound-interest credit
/// (principal + rate + planned installments), and the EN_REVISION → VIGENTE → (SUSPENDIDO) → FINALIZADO lifecycle
/// with its rejection/annulment branches. <see cref="InstallmentCount"/> and <see cref="TotalAmount"/> are DERIVED
/// (from the segments or from the amortization) and null for an indefinite plan. User ids are nullable (a non-Guid
/// principal maps to null). The applied installments, the amortization table and the projection are PR-4.
/// </summary>
public sealed record RecurringDeductionResponse(
    Guid RecurringDeductionPublicId,
    DateOnly EffectiveDate,
    string Reference,
    string RecurringDeductionTypeCode,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    string? FinancialInstitution,
    string? Observations,
    Guid AssignedPositionPublicId,
    DateOnly InstallmentStartDate,
    IReadOnlyCollection<int> ExceptionMonths,
    string CurrencyCode,
    string PayrollTypeCode,
    string InstallmentFrequencyCode,
    string ApplicationFrequencyCode,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    decimal? PrincipalAmount,
    decimal? InterestRatePercent,
    int? PlannedInstallments,
    IReadOnlyCollection<RecurringDeductionSegmentResponse> Segments,
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
    Guid ConcurrencyToken,
    // REQ-010: the audited footprints of the indebtedness overrides confirmed on this credit (empty in the normal
    // case). Additive to the contract — an FE that ignores it keeps working.
    IReadOnlyCollection<IndebtednessOverrideResponse>? IndebtednessOverrides = null)
{
    [JsonIgnore]
    public Guid Id => RecurringDeductionPublicId;
}

/// <summary>One confirmed indebtedness override: who accepted it, when, and the figures that were on screen.</summary>
public sealed record IndebtednessOverrideResponse(
    Guid IndebtednessOverridePublicId,
    string Stage,
    Guid AcknowledgedByUserId,
    DateTime AcknowledgedUtc,
    decimal BaseIncome,
    decimal MonthlyLoad,
    decimal NewInstallment,
    decimal ProjectedPercent,
    decimal LimitPercent,
    string LimitSource);

/// <summary>
/// Business fields for registering or editing a recurring deduction. The status/decision/suspension/closure fields
/// are NOT set here — they are driven by the dedicated lifecycle actions. The plan is normalized through the pure
/// <c>RecurringDeductionRules</c> in the handler (422 when incoherent). <see cref="AssignedPositionPublicId"/> is
/// optional: when omitted the employee's principal plaza is resolved (D-13 — no cost center is involved).
/// <see cref="EffectiveDate"/> MAY be in the future (D-04): the credit is registered and authorized, but no
/// installment can be charged until the date is reached.
/// </summary>
public sealed record RecurringDeductionInput(
    DateOnly EffectiveDate,
    string Reference,
    string RecurringDeductionTypeCode,
    string ConceptTypeCode,
    string? FinancialInstitution,
    string? Observations,
    Guid? AssignedPositionPublicId,
    DateOnly InstallmentStartDate,
    IReadOnlyCollection<int>? ExceptionMonths,
    string CurrencyCode,
    string PayrollTypeCode,
    string InstallmentFrequencyCode,
    string ApplicationFrequencyCode,
    bool IsIndefinite,
    bool UsesCompoundInterest,
    decimal? PrincipalAmount,
    decimal? InterestRatePercent,
    int? PlannedInstallments,
    IReadOnlyCollection<RecurringDeductionSegmentInput>? Segments,
    string SettlementActionCode,
    // REQ-010: the caller was told the credit would push the employee past the indebtedness ceiling and confirmed
    // it anyway. The levantamiento is literal — WARN, never BLOCK — so this turns the 422 into a registration with
    // an audited override footprint. Default false: a client that knows nothing about indebtedness gets the
    // warning, which is exactly what should happen.
    bool AcknowledgeIndebtednessExceeded = false);

/// <summary>
/// Compensation-concept lookup for a recurring deduction (RN-04): the name snapshot + the deduction class, so the
/// handler can reject a statutory concept (ISSS/AFP/Renta are not credits) and make the financial institution
/// mandatory for EXTERNAL deductions (P-07). Only ACTIVE, NON-STATUTORY <c>Nature = Egreso</c> concepts resolve.
/// </summary>
public sealed record DeductionConceptLookup(
    string Name,
    DeductionClass? DeductionClass);

/// <summary>
/// Result of resolving the plaza of a recurring deduction (D-13). <see cref="Found"/> is false when no assignment
/// matches. Unlike the recurring income there is NO cost center involved (P-08). Returned by the repository
/// plaza resolver.
/// </summary>
public sealed record RecurringDeductionPlazaResolution(
    bool Found,
    Guid AssignedPositionPublicId)
{
    public static readonly RecurringDeductionPlazaResolution NotFound = new(false, Guid.Empty);
}

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

public sealed record AddPersonnelFileRecurringDeductionCommand(
    Guid PersonnelFileId,
    RecurringDeductionInput Item)
    : ICommand<RecurringDeductionResponse>;

public sealed record UpdatePersonnelFileRecurringDeductionCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    RecurringDeductionInput Item,
    Guid ConcurrencyToken)
    : ICommand<RecurringDeductionResponse>;

public sealed record DeletePersonnelFileRecurringDeductionCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

/// <summary>Suspend a VIGENTE credit (<c>Suspend</c> = true, note optional) or resume a SUSPENDIDO one (false).</summary>
public sealed record SetPersonnelFileRecurringDeductionSuspensionCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    bool Suspend,
    string? Note,
    Guid ConcurrencyToken)
    : ICommand<RecurringDeductionResponse>;

/// <summary>Close an INDEFINITE VIGENTE credit by hand (→ FINALIZADO); the reason is mandatory.</summary>
public sealed record ClosePersonnelFileRecurringDeductionCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<RecurringDeductionResponse>;

/// <summary>Annul an EN_REVISION credit (→ ANULADO, HR/Manage); the reason is mandatory.</summary>
public sealed record AnnulPersonnelFileRecurringDeductionCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<RecurringDeductionResponse>;

/// <summary>Authorizer resolution of an EN_REVISION credit: <c>TargetStatusCode</c> = VIGENTE (authorize) or
/// RECHAZADO (reject — note mandatory). Double anti-self (subject / registrar).</summary>
public sealed record ResolvePersonnelFileRecurringDeductionCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    string TargetStatusCode,
    string? Note,
    Guid ConcurrencyToken,
    // REQ-010 P-14: the check runs at BOTH points, because the employee's load can change between the day the
    // credit was registered and the day it is decided. The authorizer confirms their own override.
    bool AcknowledgeIndebtednessExceeded = false)
    : ICommand<RecurringDeductionResponse>;

/// <summary>Authorizer revocation of a VIGENTE credit (→ ANULADO); the reason is mandatory. Double anti-self.</summary>
public sealed record RevokePersonnelFileRecurringDeductionCommand(
    Guid PersonnelFileId,
    Guid RecurringDeductionPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<RecurringDeductionResponse>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

public sealed record GetPersonnelFileRecurringDeductionsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<RecurringDeductionResponse>>;

public sealed record GetPersonnelFileRecurringDeductionByIdQuery(Guid PersonnelFileId, Guid RecurringDeductionPublicId)
    : IQuery<RecurringDeductionResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class RecurringDeductionSegmentInputValidator : AbstractValidator<RecurringDeductionSegmentInput>
{
    public RecurringDeductionSegmentInputValidator()
    {
        RuleFor(segment => segment.FromInstallment)
            .GreaterThanOrEqualTo(1)
            .WithMessage("FromInstallment must be greater than or equal to one.");
        RuleFor(segment => segment.ToInstallment)
            .GreaterThanOrEqualTo(segment => segment.FromInstallment)
            .When(segment => segment.ToInstallment.HasValue)
            .WithMessage("ToInstallment cannot precede FromInstallment.");
        RuleFor(segment => segment.InstallmentValue)
            .GreaterThan(0m)
            .WithMessage("InstallmentValue must be greater than zero.");
    }
}

internal sealed class RecurringDeductionInputValidator : AbstractValidator<RecurringDeductionInput>
{
    public RecurringDeductionInputValidator()
    {
        RuleFor(input => input.EffectiveDate).NotEmpty();
        RuleFor(input => input.Reference).NotEmpty().MaximumLength(PersonnelFileRecurringDeduction.MaxReferenceLength);
        RuleFor(input => input.RecurringDeductionTypeCode).NotEmpty().MaximumLength(PersonnelFileRecurringDeduction.MaxRecurringDeductionTypeCodeLength);
        RuleFor(input => input.ConceptTypeCode).NotEmpty().MaximumLength(PersonnelFileRecurringDeduction.MaxConceptTypeCodeLength);
        RuleFor(input => input.FinancialInstitution).MaximumLength(PersonnelFileRecurringDeduction.MaxFinancialInstitutionLength);
        RuleFor(input => input.Observations).MaximumLength(PersonnelFileRecurringDeduction.MaxObservationsLength);
        RuleFor(input => input.InstallmentStartDate).NotEmpty();
        RuleFor(input => input.CurrencyCode).NotEmpty().Length(PersonnelFileRecurringDeduction.MaxCurrencyCodeLength);
        RuleFor(input => input.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileRecurringDeduction.MaxPayrollTypeCodeLength);
        RuleFor(input => input.InstallmentFrequencyCode).NotEmpty().MaximumLength(PersonnelFileRecurringDeduction.MaxInstallmentFrequencyCodeLength);
        RuleFor(input => input.ApplicationFrequencyCode).NotEmpty().MaximumLength(PersonnelFileRecurringDeduction.MaxApplicationFrequencyCodeLength);

        // Every exception month is a real month (P-05). The list itself is optional (null = none).
        RuleForEach(input => input.ExceptionMonths)
            .InclusiveBetween(1, 12)
            .WithMessage("ExceptionMonths must contain month numbers between 1 and 12.");

        // Compound interest (D-08): principal + rate + count travel TOGETHER and only when the flag is on. The
        // plan coherence (interest ⇒ finite and no segments; plain ⇒ segments) is normalized in the handler
        // through RecurringDeductionRules (422 with the granular plan code).
        RuleFor(input => input.PrincipalAmount)
            .NotNull()
            .GreaterThan(0m)
            .When(input => input.UsesCompoundInterest)
            .WithMessage("PrincipalAmount is required and must be greater than zero for a compound-interest credit.");
        RuleFor(input => input.InterestRatePercent)
            .NotNull()
            .GreaterThan(0m)
            .LessThanOrEqualTo(100m)
            .When(input => input.UsesCompoundInterest)
            .WithMessage("InterestRatePercent is required and must be in (0, 100] for a compound-interest credit.");
        RuleFor(input => input.PlannedInstallments)
            .NotNull()
            .GreaterThan(0)
            .When(input => input.UsesCompoundInterest)
            .WithMessage("PlannedInstallments is required and must be greater than zero for a compound-interest credit.");

        RuleFor(input => input.PrincipalAmount)
            .Null()
            .When(input => !input.UsesCompoundInterest)
            .WithMessage("PrincipalAmount is only valid on a compound-interest credit.");
        RuleFor(input => input.InterestRatePercent)
            .Null()
            .When(input => !input.UsesCompoundInterest)
            .WithMessage("InterestRatePercent is only valid on a compound-interest credit.");
        RuleFor(input => input.PlannedInstallments)
            .Null()
            .When(input => !input.UsesCompoundInterest)
            .WithMessage("PlannedInstallments is only valid on a compound-interest credit.");

        // A plain credit is planned through its segments; a compound-interest one derives them.
        RuleFor(input => input.Segments)
            .NotEmpty()
            .When(input => !input.UsesCompoundInterest)
            .WithMessage("Segments are required for a credit without compound interest.");
        RuleFor(input => input.Segments)
            .Empty()
            .When(input => input.UsesCompoundInterest)
            .WithMessage("A compound-interest credit derives its plan and cannot carry segments.");
        RuleForEach(input => input.Segments).SetValidator(new RecurringDeductionSegmentInputValidator());

        RuleFor(input => input.SettlementActionCode)
            .NotEmpty()
            .MaximumLength(PersonnelFileRecurringDeduction.MaxSettlementActionCodeLength)
            .Must(code => code is RecurringDeductionSettlementActions.DescontarSaldo or RecurringDeductionSettlementActions.Cancelar)
            .WithMessage("SettlementActionCode must be DESCONTAR_SALDO or CANCELAR.");
    }
}

internal sealed class AddPersonnelFileRecurringDeductionCommandValidator : AbstractValidator<AddPersonnelFileRecurringDeductionCommand>
{
    public AddPersonnelFileRecurringDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new RecurringDeductionInputValidator());
    }
}

internal sealed class UpdatePersonnelFileRecurringDeductionCommandValidator : AbstractValidator<UpdatePersonnelFileRecurringDeductionCommand>
{
    public UpdatePersonnelFileRecurringDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new RecurringDeductionInputValidator());
    }
}

internal sealed class DeletePersonnelFileRecurringDeductionCommandValidator : AbstractValidator<DeletePersonnelFileRecurringDeductionCommand>
{
    public DeletePersonnelFileRecurringDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SetPersonnelFileRecurringDeductionSuspensionCommandValidator : AbstractValidator<SetPersonnelFileRecurringDeductionSuspensionCommand>
{
    public SetPersonnelFileRecurringDeductionSuspensionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Note).MaximumLength(PersonnelFileRecurringDeduction.MaxSuspensionNoteLength);
    }
}

internal sealed class ClosePersonnelFileRecurringDeductionCommandValidator : AbstractValidator<ClosePersonnelFileRecurringDeductionCommand>
{
    public ClosePersonnelFileRecurringDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileRecurringDeduction.MaxClosureReasonLength);
    }
}

internal sealed class AnnulPersonnelFileRecurringDeductionCommandValidator : AbstractValidator<AnnulPersonnelFileRecurringDeductionCommand>
{
    public AnnulPersonnelFileRecurringDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileRecurringDeduction.MaxClosureReasonLength);
    }
}

internal sealed class ResolvePersonnelFileRecurringDeductionCommandValidator : AbstractValidator<ResolvePersonnelFileRecurringDeductionCommand>
{
    public ResolvePersonnelFileRecurringDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.TargetStatusCode).NotEmpty().MaximumLength(PersonnelFileRecurringDeduction.MaxStatusCodeLength);
        RuleFor(command => command.Note).MaximumLength(PersonnelFileRecurringDeduction.MaxDecisionNoteLength);
    }
}

internal sealed class RevokePersonnelFileRecurringDeductionCommandValidator : AbstractValidator<RevokePersonnelFileRecurringDeductionCommand>
{
    public RevokePersonnelFileRecurringDeductionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecurringDeductionPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileRecurringDeduction.MaxClosureReasonLength);
    }
}

internal sealed class GetPersonnelFileRecurringDeductionsQueryValidator : AbstractValidator<GetPersonnelFileRecurringDeductionsQuery>
{
    public GetPersonnelFileRecurringDeductionsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileRecurringDeductionByIdQueryValidator : AbstractValidator<GetPersonnelFileRecurringDeductionByIdQuery>
{
    public GetPersonnelFileRecurringDeductionByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecurringDeductionPublicId).NotEmpty();
    }
}
