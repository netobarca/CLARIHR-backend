using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileRetirementRequestResponse(
    Guid RetirementRequestPublicId,
    Guid RequesterFilePublicId,
    string RequesterName,
    DateTime RequestDate,
    DateTime RetirementDate,
    string RetirementCategoryCode,
    string? RetirementCategoryName,
    string RetirementReasonCode,
    string? RetirementReasonName,
    string? Notes,
    string RequestStatusCode,
    Guid RequestedByUserId,
    Guid? ResolvedByUserId,
    DateTime? ResolutionDateUtc,
    string? ResolutionNotes,
    Guid? CanceledByUserId,
    DateTime? CancellationDateUtc,
    string? CancellationNotes,
    Guid? ExecutedByUserId,
    DateTime? ExecutionDateUtc,
    Guid? RevertedByUserId,
    DateTime? ReversalDateUtc,
    string? ReversalReason,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => RetirementRequestPublicId;
}

/// <summary>
/// Business fields HR provides when registering/editing a retirement request (D-02/D-03 — no self-service in
/// Fase 1). The status is NOT set here — it is driven by the dedicated resolution/execution/reversal actions.
/// </summary>
public sealed record RetirementRequestInput(
    Guid RequesterFilePublicId,
    DateTime RequestDate,
    DateTime RetirementDate,
    string RetirementCategoryCode,
    string RetirementReasonCode,
    string? Notes);

/// <summary>
/// Requester ("solicitante", D-02) lookup used to snapshot the name at registration and to enforce the
/// requester ≠ authorizer separation of duties at resolution (D-13, ratified).
/// </summary>
public sealed record RetirementRequesterLookup(
    Guid FilePublicId,
    string FullName,
    bool IsActive,
    Guid? LinkedUserPublicId);

/// <summary>
/// Single response mapper shared by the repository (read models / repo-side mutations) and the action
/// handlers that mutate a tracked entity in place — one projection, no drift.
/// </summary>
public static class RetirementRequestMapping
{
    public static PersonnelFileRetirementRequestResponse ToResponse(PersonnelFileRetirementRequest item) =>
        new(
            item.PublicId,
            item.RequesterFilePublicId,
            item.RequesterNameSnapshot,
            item.RequestDate,
            item.RetirementDate,
            item.RetirementCategoryCode,
            item.RetirementCategoryNameSnapshot,
            item.RetirementReasonCode,
            item.RetirementReasonNameSnapshot,
            item.Notes,
            item.RequestStatusCode,
            item.RequestedByUserId,
            item.ResolvedByUserId,
            item.ResolutionDateUtc,
            item.ResolutionNotes,
            item.CanceledByUserId,
            item.CancellationDateUtc,
            item.CancellationNotes,
            item.ExecutedByUserId,
            item.ExecutionDateUtc,
            item.RevertedByUserId,
            item.ReversalDateUtc,
            item.ReversalReason,
            item.IsActive,
            item.ConcurrencyToken);
}

public sealed record AddPersonnelFileRetirementRequestCommand(
    Guid PersonnelFileId,
    RetirementRequestInput Item)
    : ICommand<PersonnelFileRetirementRequestResponse>;

public sealed record UpdatePersonnelFileRetirementRequestCommand(
    Guid PersonnelFileId,
    Guid RetirementRequestPublicId,
    RetirementRequestInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRetirementRequestResponse>;

/// <summary>
/// Annuls a SOLICITADA request (RN-005.1 — manager). Annulment of an AUTORIZADA travels through the dedicated
/// resolution controller (<c>PATCH …/annulment</c>, AuthorizeRetirement policy) — see R-T4 of the tech plan.
/// </summary>
public sealed record CancelRetirementRequestCommand(
    Guid PersonnelFileId,
    Guid RetirementRequestPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRetirementRequestResponse>;

public sealed record GetPersonnelFileRetirementRequestsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>>;

public sealed record GetPersonnelFileRetirementRequestByIdQuery(Guid PersonnelFileId, Guid RetirementRequestPublicId)
    : IQuery<PersonnelFileRetirementRequestResponse>;

internal sealed class RetirementRequestInputValidator : AbstractValidator<RetirementRequestInput>
{
    public RetirementRequestInputValidator()
    {
        // Requester reference is mandatory (D-02); existence/active is verified in the handler (422).
        RuleFor(input => input.RequesterFilePublicId).NotEmpty();

        // Dates are mandatory; coherence (request ≤ today, retirement ≥ hire — UTC dates) is a coded
        // handler check (RETIREMENT_REQUEST_DATE_INCOHERENT, 422).
        RuleFor(input => input.RequestDate).NotEmpty();
        RuleFor(input => input.RetirementDate).NotEmpty();

        // Category/reason are mandatory; catalog existence + hierarchy are verified in the handler (422).
        RuleFor(input => input.RetirementCategoryCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.RetirementReasonCode).NotEmpty().MaximumLength(80);

        RuleFor(input => input.Notes).MaximumLength(2000);
    }
}

internal sealed class AddPersonnelFileRetirementRequestCommandValidator : AbstractValidator<AddPersonnelFileRetirementRequestCommand>
{
    public AddPersonnelFileRetirementRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new RetirementRequestInputValidator());
    }
}

internal sealed class UpdatePersonnelFileRetirementRequestCommandValidator : AbstractValidator<UpdatePersonnelFileRetirementRequestCommand>
{
    public UpdatePersonnelFileRetirementRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.RetirementRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new RetirementRequestInputValidator());
    }
}

internal sealed class CancelRetirementRequestCommandValidator : AbstractValidator<CancelRetirementRequestCommand>
{
    public CancelRetirementRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.RetirementRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

internal sealed class GetPersonnelFileRetirementRequestsQueryValidator : AbstractValidator<GetPersonnelFileRetirementRequestsQuery>
{
    public GetPersonnelFileRetirementRequestsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileRetirementRequestByIdQueryValidator : AbstractValidator<GetPersonnelFileRetirementRequestByIdQuery>
{
    public GetPersonnelFileRetirementRequestByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RetirementRequestPublicId).NotEmpty();
    }
}
