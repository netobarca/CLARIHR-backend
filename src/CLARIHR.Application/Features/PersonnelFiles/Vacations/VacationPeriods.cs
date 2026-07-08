using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// A yearly vacation fund entry of an employee (leave module D-05). Balances are derived — the fund detail
/// endpoint returns enjoyed/pending; this CRUD response carries the grants and the derived bounds only.
/// </summary>
public sealed record PersonnelFileVacationPeriodResponse(
    Guid VacationPeriodPublicId,
    int PeriodYear,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    int LegalDaysGranted,
    int BenefitDaysGranted,
    int TotalDaysGranted,
    bool GeneratesEnjoymentDays,
    bool UsedAnniversary,
    string SourceCode,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc)
{
    [JsonIgnore]
    public Guid Id => VacationPeriodPublicId;
}

/// <summary>
/// Business fields for creating a vacation fund period (manual, D-05). The bounds are derived server-side from
/// <see cref="UseAnniversary"/> (defaulting to the company preference) and the employee's primary-plaza
/// anniversary or the calendar year; the grants default to the company preference when omitted.
/// </summary>
public sealed record VacationPeriodInput(
    int PeriodYear,
    bool? UseAnniversary,
    int? LegalDaysGranted,
    int? BenefitDaysGranted,
    bool? GeneratesEnjoymentDays);

/// <summary>Body for editing the granted days of a period (only allowed while the period has no consumption).</summary>
public sealed record VacationPeriodGrantsInput(
    int LegalDaysGranted,
    int BenefitDaysGranted);

public sealed record AddPersonnelFileVacationPeriodCommand(Guid PersonnelFileId, VacationPeriodInput Item)
    : ICommand<PersonnelFileVacationPeriodResponse>;

public sealed record UpdatePersonnelFileVacationPeriodCommand(
    Guid PersonnelFileId,
    Guid VacationPeriodPublicId,
    VacationPeriodGrantsInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileVacationPeriodResponse>;

public sealed record DeletePersonnelFileVacationPeriodCommand(
    Guid PersonnelFileId,
    Guid VacationPeriodPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record GetPersonnelFileVacationPeriodsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileVacationPeriodResponse>>;

public sealed record GetPersonnelFileVacationPeriodByIdQuery(Guid PersonnelFileId, Guid VacationPeriodPublicId)
    : IQuery<PersonnelFileVacationPeriodResponse>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────

internal sealed class VacationPeriodInputValidator : AbstractValidator<VacationPeriodInput>
{
    public VacationPeriodInputValidator()
    {
        RuleFor(input => input.PeriodYear).InclusiveBetween(2000, 2100);
        RuleFor(input => input.LegalDaysGranted).GreaterThan(0).When(input => input.LegalDaysGranted.HasValue);
        RuleFor(input => input.BenefitDaysGranted).GreaterThanOrEqualTo(0).When(input => input.BenefitDaysGranted.HasValue);
    }
}

internal sealed class VacationPeriodGrantsInputValidator : AbstractValidator<VacationPeriodGrantsInput>
{
    public VacationPeriodGrantsInputValidator()
    {
        RuleFor(input => input.LegalDaysGranted).GreaterThan(0);
        RuleFor(input => input.BenefitDaysGranted).GreaterThanOrEqualTo(0);
    }
}

internal sealed class AddPersonnelFileVacationPeriodCommandValidator : AbstractValidator<AddPersonnelFileVacationPeriodCommand>
{
    public AddPersonnelFileVacationPeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new VacationPeriodInputValidator());
    }
}

internal sealed class UpdatePersonnelFileVacationPeriodCommandValidator : AbstractValidator<UpdatePersonnelFileVacationPeriodCommand>
{
    public UpdatePersonnelFileVacationPeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.VacationPeriodPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new VacationPeriodGrantsInputValidator());
    }
}

internal sealed class DeletePersonnelFileVacationPeriodCommandValidator : AbstractValidator<DeletePersonnelFileVacationPeriodCommand>
{
    public DeletePersonnelFileVacationPeriodCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.VacationPeriodPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileVacationPeriodsQueryValidator : AbstractValidator<GetPersonnelFileVacationPeriodsQuery>
{
    public GetPersonnelFileVacationPeriodsQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetPersonnelFileVacationPeriodByIdQueryValidator : AbstractValidator<GetPersonnelFileVacationPeriodByIdQuery>
{
    public GetPersonnelFileVacationPeriodByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.VacationPeriodPublicId).NotEmpty();
    }
}

/// <summary>
/// Dedicated errors for the vacation fund (leave module §5). Each code requires an EN + ES resource entry
/// (parity: <c>BackendMessageLocalizationTests</c>).
/// </summary>
internal static class VacationErrors
{
    public static readonly Error PeriodDuplicate = new(
        "VACATION_PERIOD_DUPLICATE",
        "An active vacation period already exists for this employee and year.", ErrorType.UnprocessableEntity);

    public static readonly Error PeriodHasConsumption = new(
        "VACATION_PERIOD_HAS_CONSUMPTION",
        "The vacation period cannot be edited or removed because it already has enjoyed days.", ErrorType.UnprocessableEntity);

    public static readonly Error EligibilityNotMet = new(
        "VACATION_ELIGIBILITY_NOT_MET",
        "The employee has not yet completed one year of service (Art. 177) at the start of the period.", ErrorType.UnprocessableEntity);
}
