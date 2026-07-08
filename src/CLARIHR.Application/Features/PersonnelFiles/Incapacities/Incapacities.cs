using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// One resolved segment of the incapacity breakdown surfaced on the wire (absolute chain day range, effective
/// payer after the employer-cap resolution, days and money). Deserialized from the entity's
/// <c>TrancheDetailJson</c> (jsonb) — see <see cref="IncapacityCalculationRules"/>.
/// </summary>
public sealed record IncapacityTrancheResponse(
    int DayFromAbsolute,
    int DayToAbsolute,
    decimal SubsidyPercent,
    string PayerCode,
    int Days,
    decimal Amount);

/// <summary>Non-blocking calculation warning (e.g. the employer cap was exhausted) — code + named parameters.</summary>
public sealed record IncapacityCalculationWarningResponse(
    string Code,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// An employee incapacity ("incapacidad") with its engine breakdown (days + referential amounts). The
/// <see cref="Warnings"/> collection is populated only on writes that recalculate (create / edit / close /
/// extension) and is empty on reads. Health data — the read gate is <c>ViewIncapacities</c> OR the owner.
/// </summary>
public sealed record PersonnelFileIncapacityResponse(
    Guid IncapacityPublicId,
    Guid? RequesterFilePublicId,
    string? RequesterNameSnapshot,
    string OriginCode,
    Guid RiskPublicId,
    string RiskCode,
    Guid IncapacityTypePublicId,
    string IncapacityTypeCode,
    Guid? MedicalClinicPublicId,
    Guid? AssignedPositionPublicId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodDefinitionPublicId,
    Guid? ExtendsIncapacityPublicId,
    DateOnly StartDate,
    DateOnly? EndDate,
    int CalendarDays,
    int ComputableDays,
    bool ComputableDaysOverridden,
    string? OverrideNote,
    int SubsidizedDays,
    int DiscountDays,
    int EmployerDays,
    decimal MonthlyBaseSalary,
    decimal DailySalary,
    decimal SubsidyAmount,
    decimal DiscountAmount,
    decimal EmployerAmount,
    IReadOnlyList<IncapacityTrancheResponse> Tranches,
    string StatusCode,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    IReadOnlyList<IncapacityCalculationWarningResponse> Warnings)
{
    [JsonIgnore]
    public Guid Id => IncapacityPublicId;
}

/// <summary>
/// Business fields for registering or editing an incapacity. Master references travel as public ids and are
/// resolved to their internal ids/snapshots by the handler (422 when inactive/foreign). A null
/// <see cref="EndDate"/> is an open-ended record (only when the risk allows it — D-11); the breakdown is then
/// deferred to closure. The constancia (D-22) is referenced by <see cref="DocumentFilePublicId"/> (an already
/// uploaded file with <c>purpose = IncapacityDocument</c>), mandatory when the company preference requires it.
/// </summary>
public sealed record IncapacityInput(
    Guid RiskPublicId,
    Guid IncapacityTypePublicId,
    Guid? MedicalClinicPublicId,
    Guid? AssignedPositionPublicId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodDefinitionPublicId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Notes,
    Guid? DocumentFilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentObservations);

/// <summary>Extension ("prórroga") fields: identical to <see cref="IncapacityInput"/> but the start date is
/// derived (source end date + 1, RN-04), so it is not provided by the caller.</summary>
public sealed record IncapacityExtensionInput(
    Guid RiskPublicId,
    Guid IncapacityTypePublicId,
    Guid? MedicalClinicPublicId,
    Guid? AssignedPositionPublicId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodDefinitionPublicId,
    DateOnly EndDate,
    string? Notes,
    Guid? DocumentFilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentObservations);

public sealed record AddPersonnelFileIncapacityCommand(Guid PersonnelFileId, IncapacityInput Item)
    : ICommand<PersonnelFileIncapacityResponse>;

public sealed record UpdatePersonnelFileIncapacityCommand(
    Guid PersonnelFileId,
    Guid IncapacityPublicId,
    IncapacityInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileIncapacityResponse>;

public sealed record ConfirmPersonnelFileIncapacityCommand(
    Guid PersonnelFileId,
    Guid IncapacityPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileIncapacityResponse>;

public sealed record ClosePersonnelFileIncapacityCommand(
    Guid PersonnelFileId,
    Guid IncapacityPublicId,
    DateOnly EndDate,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileIncapacityResponse>;

public sealed record AnnulPersonnelFileIncapacityCommand(
    Guid PersonnelFileId,
    Guid IncapacityPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileIncapacityResponse>;

public sealed record AddPersonnelFileIncapacityExtensionCommand(
    Guid PersonnelFileId,
    Guid SourceIncapacityPublicId,
    IncapacityExtensionInput Item)
    : ICommand<PersonnelFileIncapacityResponse>;

public sealed record GetPersonnelFileIncapacitiesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileIncapacityResponse>>;

public sealed record GetPersonnelFileIncapacityByIdQuery(Guid PersonnelFileId, Guid IncapacityPublicId)
    : IQuery<PersonnelFileIncapacityResponse>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────

internal sealed class IncapacityInputValidator : AbstractValidator<IncapacityInput>
{
    public IncapacityInputValidator()
    {
        RuleFor(input => input.RiskPublicId).NotEmpty();
        RuleFor(input => input.IncapacityTypePublicId).NotEmpty();
        RuleFor(input => input.PayrollTypeCode).MaximumLength(80);
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.StartDate).NotEmpty();
        RuleFor(input => input.EndDate)
            .GreaterThanOrEqualTo(input => input.StartDate)
            .When(input => input.EndDate.HasValue);
        RuleFor(input => input.DocumentObservations).MaximumLength(2000);
    }
}

internal sealed class IncapacityExtensionInputValidator : AbstractValidator<IncapacityExtensionInput>
{
    public IncapacityExtensionInputValidator()
    {
        RuleFor(input => input.RiskPublicId).NotEmpty();
        RuleFor(input => input.IncapacityTypePublicId).NotEmpty();
        RuleFor(input => input.PayrollTypeCode).MaximumLength(80);
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.EndDate).NotEmpty();
        RuleFor(input => input.DocumentObservations).MaximumLength(2000);
    }
}

internal sealed class AddPersonnelFileIncapacityCommandValidator : AbstractValidator<AddPersonnelFileIncapacityCommand>
{
    public AddPersonnelFileIncapacityCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new IncapacityInputValidator());
    }
}

internal sealed class UpdatePersonnelFileIncapacityCommandValidator : AbstractValidator<UpdatePersonnelFileIncapacityCommand>
{
    public UpdatePersonnelFileIncapacityCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IncapacityPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new IncapacityInputValidator());
    }
}

internal sealed class ConfirmPersonnelFileIncapacityCommandValidator : AbstractValidator<ConfirmPersonnelFileIncapacityCommand>
{
    public ConfirmPersonnelFileIncapacityCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IncapacityPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ClosePersonnelFileIncapacityCommandValidator : AbstractValidator<ClosePersonnelFileIncapacityCommand>
{
    public ClosePersonnelFileIncapacityCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IncapacityPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.EndDate).NotEmpty();
    }
}

internal sealed class AnnulPersonnelFileIncapacityCommandValidator : AbstractValidator<AnnulPersonnelFileIncapacityCommand>
{
    public AnnulPersonnelFileIncapacityCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IncapacityPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class AddPersonnelFileIncapacityExtensionCommandValidator : AbstractValidator<AddPersonnelFileIncapacityExtensionCommand>
{
    public AddPersonnelFileIncapacityExtensionCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.SourceIncapacityPublicId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new IncapacityExtensionInputValidator());
    }
}

internal sealed class GetPersonnelFileIncapacitiesQueryValidator : AbstractValidator<GetPersonnelFileIncapacitiesQuery>
{
    public GetPersonnelFileIncapacitiesQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetPersonnelFileIncapacityByIdQueryValidator : AbstractValidator<GetPersonnelFileIncapacityByIdQuery>
{
    public GetPersonnelFileIncapacityByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.IncapacityPublicId).NotEmpty();
    }
}

/// <summary>
/// Dedicated errors for employee incapacities ("incapacidades"). Each code requires an EN + ES resource entry
/// (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation lives in the validators (400).
/// </summary>
internal static class IncapacityErrors
{
    public static readonly Error RiskInvalid = new(
        "INCAPACITY_RISK_INVALID",
        "The incapacity risk does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error TypeInvalid = new(
        "INCAPACITY_TYPE_INVALID",
        "The incapacity type does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error ClinicInvalid = new(
        "INCAPACITY_CLINIC_INVALID",
        "The medical clinic does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error PayrollPeriodInvalid = new(
        "INCAPACITY_PAYROLL_PERIOD_INVALID",
        "The payroll period does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error BaseSalaryMissing = new(
        "INCAPACITY_BASE_SALARY_MISSING",
        "No base salary (SALARIO_BASE) could be resolved for the referred or principal plaza.", ErrorType.UnprocessableEntity);

    public static readonly Error EndDateRequired = new(
        "INCAPACITY_END_DATE_REQUIRED",
        "An end date is required because the selected risk does not allow an open-ended incapacity.", ErrorType.UnprocessableEntity);

    public static readonly Error Overlap = new(
        "INCAPACITY_OVERLAP",
        "The incapacity date range overlaps another active incapacity of the same employee.", ErrorType.UnprocessableEntity);

    public static readonly Error DocumentRequired = new(
        "INCAPACITY_DOCUMENT_REQUIRED",
        "A supporting document (constancia) is required to register the incapacity.", ErrorType.UnprocessableEntity);

    public static readonly Error DocumentPurposeInvalid = new(
        "INCAPACITY_DOCUMENT_PURPOSE_INVALID",
        "The referenced file was not uploaded with the incapacity-document purpose.", ErrorType.UnprocessableEntity);

    public static readonly Error ExtensionNotAllowed = new(
        "INCAPACITY_EXTENSION_NOT_ALLOWED",
        "The selected risk does not allow an extension (prórroga).", ErrorType.UnprocessableEntity);

    public static readonly Error ExtensionNotContiguous = new(
        "INCAPACITY_EXTENSION_NOT_CONTIGUOUS",
        "An extension must start on the day after the source incapacity's end date.", ErrorType.UnprocessableEntity);

    public static readonly Error ExtensionSourceInvalid = new(
        "INCAPACITY_EXTENSION_SOURCE_INVALID",
        "The source incapacity cannot be extended (annulled, open-ended, or still under review).", ErrorType.UnprocessableEntity);

    public static readonly Error ChainLocked = new(
        "INCAPACITY_CHAIN_LOCKED",
        "The incapacity cannot be edited because it already has one or more extensions; annul those first.", ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "INCAPACITY_STATE_RULE_VIOLATION",
        "The incapacity is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    public static readonly Error ConfirmSelfForbidden = new(
        "INCAPACITY_CONFIRM_SELF_FORBIDDEN",
        "An employee cannot confirm their own incapacity registration.", ErrorType.Forbidden);
}
