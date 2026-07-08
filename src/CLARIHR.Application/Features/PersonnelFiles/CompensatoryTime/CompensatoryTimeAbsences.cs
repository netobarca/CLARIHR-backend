using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// A compensatory-time absence ("ausencia / goce de tiempo compensatorio", REQ-002 D-03): a record that debits
/// hours from the employee fund. The type travels as a public id (resolved to its snapshot; a DEBITA or AMBAS
/// operation is required). The debited hours are re-verified against the fund balance under an advisory lock so
/// the balance can never go negative (RN-03). The read gate is <c>ViewCompensatoryTime</c> OR the owner employee.
/// </summary>
public sealed record PersonnelFileCompensatoryTimeAbsenceResponse(
    Guid CompensatoryTimeAbsencePublicId,
    Guid CompensatoryTimeTypePublicId,
    string CompensatoryTimeTypeCode,
    string TypeNameSnapshot,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal HoursDebited,
    string Reason,
    Guid? PayrollPeriodPublicId,
    string StatusCode,
    string? AnnulmentReason,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc)
{
    [JsonIgnore]
    public Guid Id => CompensatoryTimeAbsencePublicId;
}

/// <summary>
/// Business fields for registering or editing a compensatory-time absence. The type travels as a public id and is
/// resolved to its internal id/snapshot by the handler (422 when inactive/foreign or not a debiting operation).
/// The optional <see cref="PayrollPeriodPublicId"/> imputes the absence to a payroll period of the REQ-001 master
/// (a reference, not a containment — P-14).
/// </summary>
public sealed record CompensatoryTimeAbsenceInput(
    Guid CompensatoryTimeTypePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal HoursDebited,
    string Reason,
    Guid? PayrollPeriodPublicId,
    string? Notes);

public sealed record AddCompensatoryTimeAbsenceCommand(
    Guid PersonnelFileId,
    CompensatoryTimeAbsenceInput Item)
    : ICommand<PersonnelFileCompensatoryTimeAbsenceResponse>;

public sealed record UpdateCompensatoryTimeAbsenceCommand(
    Guid PersonnelFileId,
    Guid CompensatoryTimeAbsencePublicId,
    CompensatoryTimeAbsenceInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCompensatoryTimeAbsenceResponse>;

public sealed record AnnulCompensatoryTimeAbsenceCommand(
    Guid PersonnelFileId,
    Guid CompensatoryTimeAbsencePublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCompensatoryTimeAbsenceResponse>;

public sealed record GetCompensatoryTimeAbsencesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>>;

public sealed record GetCompensatoryTimeAbsenceByIdQuery(Guid PersonnelFileId, Guid CompensatoryTimeAbsencePublicId)
    : IQuery<PersonnelFileCompensatoryTimeAbsenceResponse>;

public sealed record GetCompensatoryTimeAbsenceHoursSuggestionQuery(
    Guid PersonnelFileId,
    DateOnly StartDate,
    DateOnly EndDate)
    : IQuery<CompensatoryTimeAbsenceHoursSuggestionResponse>;

/// <summary>
/// Suggested hours to debit for an absence range = working days (excluding the plaza rest day and the tenant
/// holidays) × the standard daily hours (RN-05 / §3.5). Advisory only — HR may override the value in the POST.
/// </summary>
public sealed record CompensatoryTimeAbsenceHoursSuggestionResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal SuggestedHours,
    decimal StandardDailyHours,
    int WorkingDays,
    int? RestDayOfWeek,
    int HolidaysExcluded);

/// <summary>
/// Whether the given range overlaps a live incapacity or a live vacation request/enjoyment of REQ-001 (RN-05).
/// Both cross-module queries are isolated in a single repository method (aclaración №6) so the degraded mode
/// (REQ-001 absent) toggles them off in one place.
/// </summary>
public sealed record CompensatoryTimeCrossOverlap(bool IncapacityOverlap, bool VacationOverlap);

// ── Validators ─────────────────────────────────────────────────────────────────────────────────

internal sealed class CompensatoryTimeAbsenceInputValidator : AbstractValidator<CompensatoryTimeAbsenceInput>
{
    public CompensatoryTimeAbsenceInputValidator()
    {
        RuleFor(input => input.CompensatoryTimeTypePublicId).NotEmpty();
        RuleFor(input => input.StartDate).NotEmpty();
        RuleFor(input => input.EndDate).NotEmpty().GreaterThanOrEqualTo(input => input.StartDate);
        RuleFor(input => input.HoursDebited).GreaterThan(0m);
        RuleFor(input => input.Reason).NotEmpty().MaximumLength(500);
        RuleFor(input => input.Notes).MaximumLength(1000);
    }
}

internal sealed class AddCompensatoryTimeAbsenceCommandValidator : AbstractValidator<AddCompensatoryTimeAbsenceCommand>
{
    public AddCompensatoryTimeAbsenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new CompensatoryTimeAbsenceInputValidator());
    }
}

internal sealed class UpdateCompensatoryTimeAbsenceCommandValidator : AbstractValidator<UpdateCompensatoryTimeAbsenceCommand>
{
    public UpdateCompensatoryTimeAbsenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.CompensatoryTimeAbsencePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new CompensatoryTimeAbsenceInputValidator());
    }
}

internal sealed class AnnulCompensatoryTimeAbsenceCommandValidator : AbstractValidator<AnnulCompensatoryTimeAbsenceCommand>
{
    public AnnulCompensatoryTimeAbsenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.CompensatoryTimeAbsencePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class GetCompensatoryTimeAbsencesQueryValidator : AbstractValidator<GetCompensatoryTimeAbsencesQuery>
{
    public GetCompensatoryTimeAbsencesQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetCompensatoryTimeAbsenceByIdQueryValidator : AbstractValidator<GetCompensatoryTimeAbsenceByIdQuery>
{
    public GetCompensatoryTimeAbsenceByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CompensatoryTimeAbsencePublicId).NotEmpty();
    }
}

internal sealed class GetCompensatoryTimeAbsenceHoursSuggestionQueryValidator
    : AbstractValidator<GetCompensatoryTimeAbsenceHoursSuggestionQuery>
{
    public GetCompensatoryTimeAbsenceHoursSuggestionQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.StartDate).NotEmpty();
        RuleFor(query => query.EndDate).NotEmpty().GreaterThanOrEqualTo(query => query.StartDate);
    }
}

/// <summary>
/// Dedicated errors for compensatory-time absences (REQ-002 §5). Each code requires an EN + ES resource entry
/// (parity: <c>BackendMessageLocalizationTests</c>). The type/state/annulment errors are reused from
/// <see cref="CompensatoryTimeCreditErrors"/> (shared codes, one resx entry).
/// </summary>
internal static class CompensatoryTimeAbsenceErrors
{
    public static readonly Error BalanceInsufficient = new(
        "COMPENSATORY_TIME_BALANCE_INSUFFICIENT",
        "The compensatory-time fund does not have enough hours to cover this absence.", ErrorType.UnprocessableEntity);

    public static readonly Error AbsenceOverlap = new(
        "COMPENSATORY_TIME_ABSENCE_OVERLAP",
        "The absence date range overlaps another active compensatory-time absence.", ErrorType.UnprocessableEntity);

    public static readonly Error IncapacityOverlap = new(
        "COMPENSATORY_TIME_INCAPACITY_OVERLAP",
        "The absence date range overlaps an active incapacity.", ErrorType.UnprocessableEntity);

    public static readonly Error VacationOverlap = new(
        "COMPENSATORY_TIME_VACATION_OVERLAP",
        "The absence date range overlaps a live vacation request or enjoyment.", ErrorType.UnprocessableEntity);

    public static readonly Error PayrollPeriodInvalid = new(
        "COMPENSATORY_TIME_PAYROLL_PERIOD_INVALID",
        "The referenced payroll period does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);
}
