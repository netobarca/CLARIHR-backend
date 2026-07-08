using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>One daily-permit schedule of a lactation period on the wire (contained in the period, non-overlapping).</summary>
public sealed record LactationScheduleResponse(
    Guid LactationSchedulePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    int DailyPermitsCount,
    int MinutesPerPermit,
    int SortOrder)
{
    [JsonIgnore]
    public Guid Id => LactationSchedulePublicId;
}

/// <summary>
/// A lactation period ("periodo de lactancia") tied to the LACTANCIA incapacity-type template, with its ordered
/// daily-permit schedules. HR-registered only (D-18, no self-service) — the record is born REGISTRADA and can
/// only be edited or annulled; it reuses the <c>incapacity-statuses</c> codes without EN_REVISION.
/// </summary>
public sealed record PersonnelFileLactationPeriodResponse(
    Guid LactationPeriodPublicId,
    Guid? RequesterFilePublicId,
    string? RequesterNameSnapshot,
    Guid IncapacityTypePublicId,
    string IncapacityTypeCode,
    DateOnly StartDate,
    DateOnly EndDate,
    string StatusCode,
    string? AnnulmentReason,
    string? Notes,
    IReadOnlyList<LactationScheduleResponse> Schedules,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc)
{
    [JsonIgnore]
    public Guid Id => LactationPeriodPublicId;
}

/// <summary>One daily-permit schedule in a create/edit request (date range → permits per day × minutes per permit).</summary>
public sealed record LactationScheduleInputDto(
    DateOnly StartDate,
    DateOnly EndDate,
    int DailyPermitsCount,
    int MinutesPerPermit);

/// <summary>
/// Business fields for registering or editing a lactation period. <see cref="IncapacityTypePublicId"/> must
/// reference the active LACTANCIA incapacity type (422 <c>LACTATION_TYPE_INVALID</c> otherwise). The full
/// schedule set travels with the request and is replaced atomically (the PUT reemplaza datos + horarios).
/// </summary>
public sealed record LactationPeriodInput(
    Guid IncapacityTypePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes,
    IReadOnlyList<LactationScheduleInputDto> Schedules);

public sealed record AddPersonnelFileLactationPeriodCommand(Guid PersonnelFileId, LactationPeriodInput Item)
    : ICommand<PersonnelFileLactationPeriodResponse>;

public sealed record UpdatePersonnelFileLactationPeriodCommand(
    Guid PersonnelFileId,
    Guid LactationPeriodPublicId,
    LactationPeriodInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileLactationPeriodResponse>;

public sealed record AnnulPersonnelFileLactationPeriodCommand(
    Guid PersonnelFileId,
    Guid LactationPeriodPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileLactationPeriodResponse>;

public sealed record GetPersonnelFileLactationPeriodsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileLactationPeriodResponse>>;

public sealed record GetPersonnelFileLactationPeriodByIdQuery(Guid PersonnelFileId, Guid LactationPeriodPublicId)
    : IQuery<PersonnelFileLactationPeriodResponse>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────

internal sealed class LactationScheduleInputDtoValidator : AbstractValidator<LactationScheduleInputDto>
{
    public LactationScheduleInputDtoValidator()
    {
        RuleFor(schedule => schedule.StartDate).NotEmpty();
        RuleFor(schedule => schedule.EndDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(schedule => schedule.StartDate);
        RuleFor(schedule => schedule.DailyPermitsCount).GreaterThanOrEqualTo(1);
        RuleFor(schedule => schedule.MinutesPerPermit).GreaterThanOrEqualTo(1);
    }
}

internal sealed class LactationPeriodInputValidator : AbstractValidator<LactationPeriodInput>
{
    public LactationPeriodInputValidator()
    {
        RuleFor(input => input.IncapacityTypePublicId).NotEmpty();
        RuleFor(input => input.StartDate).NotEmpty();
        RuleFor(input => input.EndDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(input => input.StartDate);
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.Schedules).NotNull();
        RuleForEach(input => input.Schedules).SetValidator(new LactationScheduleInputDtoValidator());
    }
}

internal sealed class AddPersonnelFileLactationPeriodCommandValidator : AbstractValidator<AddPersonnelFileLactationPeriodCommand>
{
    public AddPersonnelFileLactationPeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new LactationPeriodInputValidator());
    }
}

internal sealed class UpdatePersonnelFileLactationPeriodCommandValidator : AbstractValidator<UpdatePersonnelFileLactationPeriodCommand>
{
    public UpdatePersonnelFileLactationPeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.LactationPeriodPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new LactationPeriodInputValidator());
    }
}

internal sealed class AnnulPersonnelFileLactationPeriodCommandValidator : AbstractValidator<AnnulPersonnelFileLactationPeriodCommand>
{
    public AnnulPersonnelFileLactationPeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.LactationPeriodPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class GetPersonnelFileLactationPeriodsQueryValidator : AbstractValidator<GetPersonnelFileLactationPeriodsQuery>
{
    public GetPersonnelFileLactationPeriodsQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetPersonnelFileLactationPeriodByIdQueryValidator : AbstractValidator<GetPersonnelFileLactationPeriodByIdQuery>
{
    public GetPersonnelFileLactationPeriodByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.LactationPeriodPublicId).NotEmpty();
    }
}

/// <summary>
/// Dedicated errors for lactation periods (part of the incapacities module — permission <c>ManageIncapacities</c> /
/// <c>ViewIncapacities</c>). Each code requires an EN + ES resource entry (parity:
/// <c>BackendMessageLocalizationTests</c>). The schedule containment/overlap rules are enforced both here (clean
/// 422) and by the domain <c>ReplaceSchedules</c> guard (defense in depth).
/// </summary>
internal static class LactationErrors
{
    public static readonly Error TypeInvalid = new(
        "LACTATION_TYPE_INVALID",
        "The referenced type must be the active LACTANCIA incapacity type.", ErrorType.UnprocessableEntity);

    public static readonly Error ScheduleOutOfRange = new(
        "LACTATION_SCHEDULE_OUT_OF_RANGE",
        "Every lactation schedule must be contained within the lactation period.", ErrorType.UnprocessableEntity);

    public static readonly Error ScheduleOverlap = new(
        "LACTATION_SCHEDULE_OVERLAP",
        "The lactation schedules must not overlap each other.", ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "LACTATION_STATE_RULE_VIOLATION",
        "The lactation period is not in a state that allows this operation.", ErrorType.UnprocessableEntity);
}
