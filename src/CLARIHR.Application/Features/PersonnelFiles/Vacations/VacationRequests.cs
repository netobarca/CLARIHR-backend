using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>One fund-period allocation of an approved request on the wire (which period, its year and the days).</summary>
public sealed record VacationAllocationResponse(
    Guid VacationPeriodPublicId,
    int PeriodYear,
    int Days);

/// <summary>One total/partial return of a request on the wire, with the LIFO period distribution.</summary>
public sealed record VacationReturnResponse(
    Guid ReturnPublicId,
    int Days,
    DateTime ReturnDateUtc,
    string? Reason,
    IReadOnlyList<VacationAllocationResponse> Distribution);

/// <summary>
/// An employee vacation request ("solicitud de vacaciones", leave module D-13/D-14). Born SOLICITADA; a HR
/// decision moves it to APROBADA (with the fund allocations, FIFO by default) or RECHAZADA; the owner may cancel
/// it while SOLICITADA; total/partial returns (LIFO by default) walk it through DEVUELTA_PARCIAL to DEVUELTA.
/// </summary>
public sealed record PersonnelFileVacationRequestResponse(
    Guid VacationRequestPublicId,
    Guid? RequesterFilePublicId,
    string? RequesterNameSnapshot,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequestedDays,
    string StatusCode,
    Guid? PlanLinePublicId,
    string? DecisionNotes,
    DateTime? DecisionDateUtc,
    int ConsumedDays,
    int ReturnedDays,
    int NetConsumedDays,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    IReadOnlyList<VacationAllocationResponse> Allocations,
    IReadOnlyList<VacationReturnResponse> Returns)
{
    [JsonIgnore]
    public Guid Id => VacationRequestPublicId;
}

/// <summary>Business fields for creating a vacation request. <see cref="RequestedDays"/> is the number of enjoyed
/// (business) days; the date range is validated against Art. 178 (RN-27).</summary>
public sealed record VacationRequestInput(
    DateOnly StartDate,
    DateOnly EndDate,
    int RequestedDays,
    Guid? PlanLinePublicId,
    string? Notes);

/// <summary>One editable fund-period allocation supplied when approving a request (period publicId + days).</summary>
public sealed record VacationAllocationItem(Guid VacationPeriodPublicId, int Days);

/// <summary>
/// Decision on a SOLICITADA request. When <see cref="Approve"/> is true the request is approved against
/// <see cref="Allocations"/> (Σ = requested days); an empty/omitted set uses the FIFO suggestion. When false the
/// request is rejected (the notes are optional).
/// </summary>
public sealed record VacationDecisionInput(
    bool Approve,
    IReadOnlyCollection<VacationAllocationItem>? Allocations,
    string? Notes);

/// <summary>One editable period → days entry of a return distribution (period publicId + days).</summary>
public sealed record VacationReturnDistributionItem(Guid VacationPeriodPublicId, int Days);

/// <summary>
/// A total/partial return of enjoyed days. <see cref="Distribution"/> reverses the days to their periods of
/// origin; an empty/omitted set uses the LIFO suggestion.
/// </summary>
public sealed record VacationReturnInput(
    int Days,
    string? Reason,
    IReadOnlyCollection<VacationReturnDistributionItem>? Distribution);

public sealed record AddPersonnelFileVacationRequestCommand(Guid PersonnelFileId, VacationRequestInput Item)
    : ICommand<PersonnelFileVacationRequestResponse>;

public sealed record DecidePersonnelFileVacationRequestCommand(
    Guid PersonnelFileId,
    Guid VacationRequestPublicId,
    VacationDecisionInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileVacationRequestResponse>;

public sealed record CancelPersonnelFileVacationRequestCommand(
    Guid PersonnelFileId,
    Guid VacationRequestPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileVacationRequestResponse>;

public sealed record AddPersonnelFileVacationReturnCommand(
    Guid PersonnelFileId,
    Guid VacationRequestPublicId,
    VacationReturnInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileVacationRequestResponse>;

public sealed record GetPersonnelFileVacationRequestsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileVacationRequestResponse>>;

public sealed record GetPersonnelFileVacationRequestByIdQuery(Guid PersonnelFileId, Guid VacationRequestPublicId)
    : IQuery<PersonnelFileVacationRequestResponse>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────

internal sealed class VacationRequestInputValidator : AbstractValidator<VacationRequestInput>
{
    public VacationRequestInputValidator()
    {
        RuleFor(input => input.StartDate).NotEmpty();
        RuleFor(input => input.EndDate).GreaterThanOrEqualTo(input => input.StartDate);
        RuleFor(input => input.RequestedDays).GreaterThan(0);
        RuleFor(input => input.Notes).MaximumLength(1000);
    }
}

internal sealed class VacationAllocationItemValidator : AbstractValidator<VacationAllocationItem>
{
    public VacationAllocationItemValidator()
    {
        RuleFor(item => item.VacationPeriodPublicId).NotEmpty();
        RuleFor(item => item.Days).GreaterThan(0);
    }
}

internal sealed class VacationDecisionInputValidator : AbstractValidator<VacationDecisionInput>
{
    public VacationDecisionInputValidator()
    {
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleForEach(input => input.Allocations).SetValidator(new VacationAllocationItemValidator())
            .When(input => input.Allocations is not null);
    }
}

internal sealed class VacationReturnDistributionItemValidator : AbstractValidator<VacationReturnDistributionItem>
{
    public VacationReturnDistributionItemValidator()
    {
        RuleFor(item => item.VacationPeriodPublicId).NotEmpty();
        RuleFor(item => item.Days).GreaterThan(0);
    }
}

internal sealed class VacationReturnInputValidator : AbstractValidator<VacationReturnInput>
{
    public VacationReturnInputValidator()
    {
        RuleFor(input => input.Days).GreaterThan(0);
        RuleFor(input => input.Reason).MaximumLength(500);
        RuleForEach(input => input.Distribution).SetValidator(new VacationReturnDistributionItemValidator())
            .When(input => input.Distribution is not null);
    }
}

internal sealed class AddPersonnelFileVacationRequestCommandValidator : AbstractValidator<AddPersonnelFileVacationRequestCommand>
{
    public AddPersonnelFileVacationRequestCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new VacationRequestInputValidator());
    }
}

internal sealed class DecidePersonnelFileVacationRequestCommandValidator : AbstractValidator<DecidePersonnelFileVacationRequestCommand>
{
    public DecidePersonnelFileVacationRequestCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.VacationRequestPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new VacationDecisionInputValidator());
    }
}

internal sealed class CancelPersonnelFileVacationRequestCommandValidator : AbstractValidator<CancelPersonnelFileVacationRequestCommand>
{
    public CancelPersonnelFileVacationRequestCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.VacationRequestPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AddPersonnelFileVacationReturnCommandValidator : AbstractValidator<AddPersonnelFileVacationReturnCommand>
{
    public AddPersonnelFileVacationReturnCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.VacationRequestPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new VacationReturnInputValidator());
    }
}

internal sealed class GetPersonnelFileVacationRequestsQueryValidator : AbstractValidator<GetPersonnelFileVacationRequestsQuery>
{
    public GetPersonnelFileVacationRequestsQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetPersonnelFileVacationRequestByIdQueryValidator : AbstractValidator<GetPersonnelFileVacationRequestByIdQuery>
{
    public GetPersonnelFileVacationRequestByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.VacationRequestPublicId).NotEmpty();
    }
}
