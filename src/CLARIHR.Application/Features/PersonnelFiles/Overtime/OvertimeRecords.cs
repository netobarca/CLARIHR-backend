using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// An overtime record of a personnel file ("hora extra del empleado", REQ-007): the shift (work date, overtime
/// type + code + name snapshot, the type factor snapshot + the applied factor with its mandatory override note,
/// the h:m duration with the derived decimal hours, the optional start/end time), the motive (justification type
/// + code + name snapshot, observations), the origin channel (RRHH / PORTAL — the dual channel of P-01), the
/// mandatory plaza, the requester trío (file + name snapshot), the payroll destination and the EN_REVISION →
/// AUTORIZADA → APLICADA lifecycle with its rejection / annulment branches. User ids are nullable (a non-Guid
/// principal maps to null — lesson REQ-003). Applications history + projection are PR-4/PR-5.
/// </summary>
public sealed record OvertimeRecordResponse(
    Guid OvertimeRecordPublicId,
    DateOnly WorkDate,
    Guid OvertimeTypePublicId,
    string OvertimeTypeCodeSnapshot,
    string OvertimeTypeNameSnapshot,
    decimal TypeFactorSnapshot,
    decimal FactorApplied,
    string? FactorOverrideNote,
    int DurationHours,
    int DurationMinutes,
    decimal DurationDecimalHours,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid JustificationTypePublicId,
    string JustificationCodeSnapshot,
    string JustificationNameSnapshot,
    string? Observations,
    string OriginChannel,
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
    Guid? AnnulledBySettlementPublicId,
    Guid? AppliedBySettlementPublicId,
    Guid? CompensatedByCreditPublicId,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => OvertimeRecordPublicId;
}

/// <summary>
/// Business fields for registering or editing an overtime record. The status/decision/annulment fields are NOT
/// set here — they are driven by the dedicated lifecycle actions. The factor coherence (override note) + the
/// duration + the sanity cap + the daily cap are validated through the pure <c>OvertimeRecordRules</c> in the
/// handler (422 when incoherent). <see cref="FactorApplied"/> is optional: it defaults to the type's reference
/// factor when omitted (no override note needed). <see cref="AssignedPositionPublicId"/> is optional: the
/// employee's principal plaza is resolved when omitted (D-12). <see cref="RequesterFilePublicId"/> (the trío) is
/// required on the RRHH channel; on the PORTAL self-service channel it is ignored (the requester is the subject
/// employee). <see cref="PayrollPeriodLabel"/> is mandatory (degraded mode — no hard period FK in PR-3).
/// </summary>
public sealed record OvertimeRecordInput(
    DateOnly WorkDate,
    Guid OvertimeTypePublicId,
    decimal? FactorApplied,
    string? FactorOverrideNote,
    int DurationHours,
    int DurationMinutes,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid JustificationTypePublicId,
    string? Observations,
    Guid? AssignedPositionPublicId,
    Guid? RequesterFilePublicId,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>Payroll destination fields for the re-imputation ("enviar a otro periodo", RF-005 — only AUTORIZADA).</summary>
public sealed record OvertimeRecordPeriodInput(
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate);

/// <summary>
/// Result of resolving the plaza of an overtime record (D-12). <see cref="Found"/> is false when no assignment
/// matches; the overtime module persists no cost center (§0.1). Returned by the repository plaza resolver.
/// </summary>
public sealed record OvertimePlazaResolution(bool Found, Guid AssignedPositionPublicId)
{
    public static readonly OvertimePlazaResolution NotFound = new(false, Guid.Empty);
}

/// <summary>Company overtime-type master lookup (RN-19): the code + name + reference factor + activity snapshot.</summary>
public sealed record OvertimeTypeLookup(Guid PublicId, string Code, string Name, decimal DefaultFactor, bool IsActive);

/// <summary>Company overtime justification-type master lookup (RF-003): the code + name + activity snapshot.</summary>
public sealed record OvertimeJustificationLookup(Guid PublicId, string Code, string Name, bool IsActive);

/// <summary>
/// Requester lookup for the trío (№6/№10) + the TRIPLE anti-self pata (c): the display name, activity and
/// linked login of a personnel file of the company.
/// </summary>
public sealed record OvertimeRequesterLookup(
    Guid FilePublicId,
    string FullName,
    bool IsActive,
    Guid? LinkedUserPublicId);

/// <summary>
/// The company overtime preferences (REQ-007 P-01/P-05): whether the employee self-service portal channel is
/// enabled (null = off) and the optional daily cap in minutes (null = no cap). Read from
/// <c>CompanyPreference</c>; a company without a preference row defaults to <c>(false, null)</c>.
/// </summary>
public sealed record OvertimeCompanyPreferences(bool SelfServiceEnabled, int? MaxDailyMinutes);

// ── Commands ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>Register an overtime record (EN_REVISION). Dual channel (P-01): HR (Manage) or the employee portal.</summary>
public sealed record AddPersonnelFileOvertimeRecordCommand(
    Guid PersonnelFileId,
    OvertimeRecordInput Item)
    : ICommand<OvertimeRecordResponse>;

public sealed record UpdatePersonnelFileOvertimeRecordCommand(
    Guid PersonnelFileId,
    Guid OvertimeRecordPublicId,
    OvertimeRecordInput Item,
    Guid ConcurrencyToken)
    : ICommand<OvertimeRecordResponse>;

public sealed record DeletePersonnelFileOvertimeRecordCommand(
    Guid PersonnelFileId,
    Guid OvertimeRecordPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

/// <summary>Annul (retiro) an EN_REVISION overtime record (→ ANULADA); the reason is mandatory. Dual channel.</summary>
public sealed record AnnulPersonnelFileOvertimeRecordCommand(
    Guid PersonnelFileId,
    Guid OvertimeRecordPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OvertimeRecordResponse>;

/// <summary>Re-target ("enviar a otro periodo", RF-005) the payroll destination of an AUTORIZADA record (Manage only).</summary>
public sealed record RetargetPersonnelFileOvertimeRecordPeriodCommand(
    Guid PersonnelFileId,
    Guid OvertimeRecordPublicId,
    OvertimeRecordPeriodInput Period,
    Guid ConcurrencyToken)
    : ICommand<OvertimeRecordResponse>;

/// <summary>Authorizer resolution of an EN_REVISION record: <c>TargetStatusCode</c> = AUTORIZADA (authorize) or
/// RECHAZADA (reject — note mandatory). TRIPLE anti-self (subject / registrar / requester) — aclaración №6.</summary>
public sealed record ResolvePersonnelFileOvertimeRecordCommand(
    Guid PersonnelFileId,
    Guid OvertimeRecordPublicId,
    string TargetStatusCode,
    string? Note,
    Guid ConcurrencyToken)
    : ICommand<OvertimeRecordResponse>;

/// <summary>Authorizer revocation of an AUTORIZADA record (→ ANULADA); the reason is mandatory. TRIPLE anti-self.</summary>
public sealed record RevokePersonnelFileOvertimeRecordCommand(
    Guid PersonnelFileId,
    Guid OvertimeRecordPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<OvertimeRecordResponse>;

// ── Queries ───────────────────────────────────────────────────────────────────────────────────────

public sealed record GetPersonnelFileOvertimeRecordsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<OvertimeRecordResponse>>;

public sealed record GetPersonnelFileOvertimeRecordByIdQuery(Guid PersonnelFileId, Guid OvertimeRecordPublicId)
    : IQuery<OvertimeRecordResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────

internal sealed class OvertimeRecordInputValidator : AbstractValidator<OvertimeRecordInput>
{
    public OvertimeRecordInputValidator()
    {
        RuleFor(input => input.WorkDate).NotEmpty();
        RuleFor(input => input.OvertimeTypePublicId).NotEmpty();
        RuleFor(input => input.JustificationTypePublicId).NotEmpty();

        // The factor coherence (override note) + the duration bounds (minutes 0–59, positive total) are shaped by
        // the pure OvertimeRecordRules in the handler (422 with the granular code); here only the positivity of the
        // supplied factor is validated (400). The duration is NOT bounds-checked here so an invalid minute value
        // (e.g. 65) surfaces as a 422 duration code rather than a 400.
        RuleFor(input => input.FactorApplied).GreaterThan(0m).When(input => input.FactorApplied.HasValue);
        RuleFor(input => input.DurationHours).GreaterThanOrEqualTo(0);
        RuleFor(input => input.FactorOverrideNote).MaximumLength(PersonnelFileOvertimeRecord.MaxFactorOverrideNoteLength);
        RuleFor(input => input.Observations).MaximumLength(PersonnelFileOvertimeRecord.MaxObservationsLength);

        RuleFor(input => input.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOvertimeRecord.MaxPayrollTypeCodeLength);
        RuleFor(input => input.PayrollPeriodLabel).NotEmpty().MaximumLength(PersonnelFileOvertimeRecord.MaxPayrollPeriodLabelLength);
    }
}

internal sealed class OvertimeRecordPeriodInputValidator : AbstractValidator<OvertimeRecordPeriodInput>
{
    public OvertimeRecordPeriodInputValidator()
    {
        RuleFor(input => input.PayrollTypeCode).NotEmpty().MaximumLength(PersonnelFileOvertimeRecord.MaxPayrollTypeCodeLength);
        RuleFor(input => input.PayrollPeriodLabel).NotEmpty().MaximumLength(PersonnelFileOvertimeRecord.MaxPayrollPeriodLabelLength);
    }
}

internal sealed class AddPersonnelFileOvertimeRecordCommandValidator : AbstractValidator<AddPersonnelFileOvertimeRecordCommand>
{
    public AddPersonnelFileOvertimeRecordCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new OvertimeRecordInputValidator());
    }
}

internal sealed class UpdatePersonnelFileOvertimeRecordCommandValidator : AbstractValidator<UpdatePersonnelFileOvertimeRecordCommand>
{
    public UpdatePersonnelFileOvertimeRecordCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OvertimeRecordPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new OvertimeRecordInputValidator());
    }
}

internal sealed class DeletePersonnelFileOvertimeRecordCommandValidator : AbstractValidator<DeletePersonnelFileOvertimeRecordCommand>
{
    public DeletePersonnelFileOvertimeRecordCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OvertimeRecordPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AnnulPersonnelFileOvertimeRecordCommandValidator : AbstractValidator<AnnulPersonnelFileOvertimeRecordCommand>
{
    public AnnulPersonnelFileOvertimeRecordCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OvertimeRecordPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOvertimeRecord.MaxAnnulmentReasonLength);
    }
}

internal sealed class RetargetPersonnelFileOvertimeRecordPeriodCommandValidator : AbstractValidator<RetargetPersonnelFileOvertimeRecordPeriodCommand>
{
    public RetargetPersonnelFileOvertimeRecordPeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OvertimeRecordPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Period).NotNull().SetValidator(new OvertimeRecordPeriodInputValidator());
    }
}

internal sealed class ResolvePersonnelFileOvertimeRecordCommandValidator : AbstractValidator<ResolvePersonnelFileOvertimeRecordCommand>
{
    public ResolvePersonnelFileOvertimeRecordCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OvertimeRecordPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.TargetStatusCode).NotEmpty().MaximumLength(PersonnelFileOvertimeRecord.MaxStatusCodeLength);
        RuleFor(command => command.Note).MaximumLength(PersonnelFileOvertimeRecord.MaxDecisionNoteLength);
    }
}

internal sealed class RevokePersonnelFileOvertimeRecordCommandValidator : AbstractValidator<RevokePersonnelFileOvertimeRecordCommand>
{
    public RevokePersonnelFileOvertimeRecordCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OvertimeRecordPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileOvertimeRecord.MaxAnnulmentReasonLength);
    }
}

internal sealed class GetPersonnelFileOvertimeRecordsQueryValidator : AbstractValidator<GetPersonnelFileOvertimeRecordsQuery>
{
    public GetPersonnelFileOvertimeRecordsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileOvertimeRecordByIdQueryValidator : AbstractValidator<GetPersonnelFileOvertimeRecordByIdQuery>
{
    public GetPersonnelFileOvertimeRecordByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OvertimeRecordPublicId).NotEmpty();
    }
}
