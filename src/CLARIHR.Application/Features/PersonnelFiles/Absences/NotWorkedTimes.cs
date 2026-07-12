using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.Absences;

// ── Contracts ─────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One recorded not-worked-time (REQ-011). The amount is NEVER typed: the server computes it.</summary>
public sealed record NotWorkedTimeResponse(
    Guid NotWorkedTimePublicId,
    Guid AssignedPositionPublicId,
    string TypeCode,
    string TypeName,
    bool UsesWorkSchedule,
    decimal DiscountPercent,
    string? DeductionConceptTypeCode,
    string? IncomeConceptTypeCode,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Hours,
    string? Reason,
    string OriginCode,
    int CalendarDays,
    int ComputableDays,
    int SeventhDayPenaltyDays,
    decimal DiscountedDays,
    decimal DailySalary,
    decimal DiscountAmount,
    string CurrencyCode,
    string StatusCode,
    Guid? RegisteredByUserId,
    DateTime RegisteredUtc,
    Guid? AnnulledByUserId,
    DateTime? AnnulledUtc,
    string? AnnulmentReason,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => NotWorkedTimePublicId;
}

public sealed record AddNotWorkedTimeCommand(
    Guid PersonnelFileId,
    string TypeCode,
    Guid? AssignedPositionPublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Hours,
    string? Reason) : ICommand<NotWorkedTimeResponse>;

public sealed record AnnulNotWorkedTimeCommand(
    Guid PersonnelFileId,
    Guid NotWorkedTimePublicId,
    string Reason,
    Guid ConcurrencyToken) : ICommand<NotWorkedTimeResponse>;

public sealed record GetNotWorkedTimesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<NotWorkedTimeResponse>>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────────

internal sealed class AddNotWorkedTimeCommandValidator : AbstractValidator<AddNotWorkedTimeCommand>
{
    public AddNotWorkedTimeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.TypeCode).NotEmpty().MaximumLength(PersonnelFileNotWorkedTime.MaxTypeCodeLength);
        RuleFor(command => command.Hours).GreaterThan(0m).When(command => command.Hours.HasValue);
        RuleFor(command => command.Reason).MaximumLength(PersonnelFileNotWorkedTime.MaxReasonLength);
    }
}

internal sealed class AnnulNotWorkedTimeCommandValidator : AbstractValidator<AnnulNotWorkedTimeCommand>
{
    public AnnulNotWorkedTimeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.NotWorkedTimePublicId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(PersonnelFileNotWorkedTime.MaxAnnulmentReasonLength);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetNotWorkedTimesQueryValidator : AbstractValidator<GetNotWorkedTimesQuery>
{
    public GetNotWorkedTimesQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}
