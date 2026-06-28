using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileEconomicAidRequestResponse(
    Guid EconomicAidRequestPublicId,
    string EconomicAidTypeCode,
    string? TypeName,
    string RequestStatusCode,
    string Description,
    decimal RequestedAmount,
    string CurrencyCode,
    DateTime RequestDateUtc,
    Guid RequestedByUserId,
    decimal? ApprovedAmount,
    Guid? ResolvedByUserId,
    DateTime? ResolutionDateUtc,
    string? ResolutionNotes,
    int? ResponseTimeDays,
    decimal? DisbursedAmount,
    DateTime? DisbursementDateUtc,
    string? PaymentMethodCode,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => EconomicAidRequestPublicId;
}

/// <summary>
/// Business fields an employee (self-service) or HR provides when requesting economic aid. The status, resolution
/// and disbursement are NOT set here — they are driven by the dedicated validation actions (PR-5).
/// </summary>
public sealed record EconomicAidRequestInput(
    string TypeCode,
    string Description,
    decimal RequestedAmount,
    string? CurrencyCode,
    DateTime RequestDateUtc);

public sealed record AddPersonnelFileEconomicAidRequestCommand(
    Guid PersonnelFileId,
    EconomicAidRequestInput Item)
    : ICommand<PersonnelFileEconomicAidRequestResponse>;

public sealed record UpdatePersonnelFileEconomicAidRequestCommand(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    EconomicAidRequestInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEconomicAidRequestResponse>;

public sealed record DeletePersonnelFileEconomicAidRequestCommand(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record GetPersonnelFileEconomicAidRequestsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>>;

public sealed record GetPersonnelFileEconomicAidRequestByIdQuery(Guid PersonnelFileId, Guid EconomicAidRequestPublicId)
    : IQuery<PersonnelFileEconomicAidRequestResponse>;

internal sealed class EconomicAidRequestInputValidator : AbstractValidator<EconomicAidRequestInput>
{
    public EconomicAidRequestInputValidator()
    {
        // Type is mandatory; existence/active is verified against the catalog in the handler (422).
        RuleFor(input => input.TypeCode).NotEmpty().MaximumLength(80);

        // The emergency reason is mandatory (sensitive data, D-10).
        RuleFor(input => input.Description).NotEmpty().MaximumLength(2000);

        // Requested amount must be positive (D-05).
        RuleFor(input => input.RequestedAmount)
            .GreaterThan(0)
            .WithMessage("RequestedAmount must be greater than zero.");

        // Currency ISO-4217 (3 chars) — house convention validates length; default resolved in the handler.
        RuleFor(input => input.CurrencyCode)
            .Length(3)
            .When(input => !string.IsNullOrWhiteSpace(input.CurrencyCode));

        // Request date required and not in the future (RN-07).
        RuleFor(input => input.RequestDateUtc)
            .NotEmpty()
            .Must(date => date <= DateTime.UtcNow.AddDays(1))
            .WithMessage("RequestDateUtc must not be in the future.");
    }
}

internal sealed class AddPersonnelFileEconomicAidRequestCommandValidator : AbstractValidator<AddPersonnelFileEconomicAidRequestCommand>
{
    public AddPersonnelFileEconomicAidRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new EconomicAidRequestInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEconomicAidRequestCommandValidator : AbstractValidator<UpdatePersonnelFileEconomicAidRequestCommand>
{
    public UpdatePersonnelFileEconomicAidRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new EconomicAidRequestInputValidator());
    }
}

internal sealed class DeletePersonnelFileEconomicAidRequestCommandValidator : AbstractValidator<DeletePersonnelFileEconomicAidRequestCommand>
{
    public DeletePersonnelFileEconomicAidRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEconomicAidRequestsQueryValidator : AbstractValidator<GetPersonnelFileEconomicAidRequestsQuery>
{
    public GetPersonnelFileEconomicAidRequestsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEconomicAidRequestByIdQueryValidator : AbstractValidator<GetPersonnelFileEconomicAidRequestByIdQuery>
{
    public GetPersonnelFileEconomicAidRequestByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EconomicAidRequestPublicId).NotEmpty();
    }
}

// --- Validation actions (HR), forward-compatible with a future approval flow (RF-011) ---

/// <summary>HR validation (D-03): move a pending request to a resolution target with an optional approved amount.</summary>
public sealed record ResolveEconomicAidRequestCommand(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    string TargetStatusCode,
    decimal? ApprovedAmount,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEconomicAidRequestResponse>;

/// <summary>Informational disbursement of an approved request (D-09).</summary>
public sealed record DisburseEconomicAidRequestCommand(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    decimal DisbursedAmount,
    DateTime DisbursementDateUtc,
    string? PaymentMethodCode,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEconomicAidRequestResponse>;

/// <summary>Cancel/withdraw a pending request (self-service for the owner, or HR) — D-11.</summary>
public sealed record CancelEconomicAidRequestCommand(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEconomicAidRequestResponse>;

internal sealed class ResolveEconomicAidRequestCommandValidator : AbstractValidator<ResolveEconomicAidRequestCommand>
{
    public ResolveEconomicAidRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.TargetStatusCode).NotEmpty().MaximumLength(80);
        RuleFor(c => c.ApprovedAmount)
            .GreaterThan(0)
            .When(c => c.ApprovedAmount.HasValue)
            .WithMessage("ApprovedAmount must be greater than zero.");
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

internal sealed class DisburseEconomicAidRequestCommandValidator : AbstractValidator<DisburseEconomicAidRequestCommand>
{
    public DisburseEconomicAidRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.DisbursedAmount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.DisbursementDateUtc).NotEmpty();
        RuleFor(c => c.PaymentMethodCode).MaximumLength(80);
    }
}

internal sealed class CancelEconomicAidRequestCommandValidator : AbstractValidator<CancelEconomicAidRequestCommand>
{
    public CancelEconomicAidRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}
