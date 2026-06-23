using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileAuthorizationSubstitutionResponse(
    Guid AuthorizationSubstitutionPublicId,
    string SubstitutionTypeCode,
    Guid SubstitutePersonnelFileId,
    Guid SubstitutePositionSlotPublicId,
    // Snapshot of the substitute's position title at designation time (D-02), for history/UI display.
    string? SubstitutePositionTitle,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => AuthorizationSubstitutionPublicId;
}

public sealed record AuthorizationSubstitutionInput(
    string SubstitutionTypeCode,
    Guid SubstitutePersonnelFileId,
    Guid SubstitutePositionSlotPublicId,
    DateTime StartDate,
    // Nullable in the input so a missing end date is rejected by the validator (400) rather than defaulting (D-03).
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileAuthorizationSubstitutionCommand(
    Guid PersonnelFileId,
    AuthorizationSubstitutionInput Item)
    : ICommand<PersonnelFileAuthorizationSubstitutionResponse>;

public sealed record UpdatePersonnelFileAuthorizationSubstitutionCommand(
    Guid PersonnelFileId,
    Guid AuthorizationSubstitutionPublicId,
    AuthorizationSubstitutionInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAuthorizationSubstitutionResponse>;

public sealed record DeletePersonnelFileAuthorizationSubstitutionCommand(
    Guid PersonnelFileId,
    Guid AuthorizationSubstitutionPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileAuthorizationSubstitutionPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileAuthorizationSubstitutionCommand(
    Guid PersonnelFileId,
    Guid AuthorizationSubstitutionPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionPatchOperation> Operations)
    : ICommand<PersonnelFileAuthorizationSubstitutionResponse>;

public sealed record GetPersonnelFileAuthorizationSubstitutionsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>;

public sealed record GetPersonnelFileAuthorizationSubstitutionByIdQuery(Guid PersonnelFileId, Guid AuthorizationSubstitutionPublicId)
    : IQuery<PersonnelFileAuthorizationSubstitutionResponse>;

internal sealed class AuthorizationSubstitutionInputValidator : AbstractValidator<AuthorizationSubstitutionInput>
{
    public AuthorizationSubstitutionInputValidator()
    {
        RuleFor(input => input.SubstitutionTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.SubstitutePersonnelFileId).NotEmpty();
        RuleFor(input => input.SubstitutePositionSlotPublicId).NotEmpty();
        RuleFor(input => input.EndDate).NotNull();
        RuleFor(input => input.StartDate).LessThanOrEqualTo(input => input.EndDate!.Value).When(input => input.EndDate.HasValue);
    }
}

internal sealed class AddPersonnelFileAuthorizationSubstitutionCommandValidator : AbstractValidator<AddPersonnelFileAuthorizationSubstitutionCommand>
{
    public AddPersonnelFileAuthorizationSubstitutionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AuthorizationSubstitutionInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAuthorizationSubstitutionCommandValidator : AbstractValidator<UpdatePersonnelFileAuthorizationSubstitutionCommand>
{
    public UpdatePersonnelFileAuthorizationSubstitutionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AuthorizationSubstitutionPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AuthorizationSubstitutionInputValidator());
    }
}

internal sealed class DeletePersonnelFileAuthorizationSubstitutionCommandValidator : AbstractValidator<DeletePersonnelFileAuthorizationSubstitutionCommand>
{
    public DeletePersonnelFileAuthorizationSubstitutionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AuthorizationSubstitutionPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileAuthorizationSubstitutionCommandValidator : AbstractValidator<PatchPersonnelFileAuthorizationSubstitutionCommand>
{
    public PatchPersonnelFileAuthorizationSubstitutionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AuthorizationSubstitutionPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Operations).NotEmpty();
        RuleFor(c => c.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(c => c.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class GetPersonnelFileAuthorizationSubstitutionsQueryValidator : AbstractValidator<GetPersonnelFileAuthorizationSubstitutionsQuery>
{
    public GetPersonnelFileAuthorizationSubstitutionsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAuthorizationSubstitutionByIdQueryValidator : AbstractValidator<GetPersonnelFileAuthorizationSubstitutionByIdQuery>
{
    public GetPersonnelFileAuthorizationSubstitutionByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.AuthorizationSubstitutionPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileAuthorizationSubstitutionPatchState
{
    public string SubstitutionTypeCode { get; set; } = string.Empty;
    public Guid SubstitutePersonnelFileId { get; set; }
    public Guid SubstitutePositionSlotId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileAuthorizationSubstitutionPatchState From(PersonnelFileAuthorizationSubstitutionResponse response) =>
        new()
        {
            SubstitutionTypeCode = response.SubstitutionTypeCode,
            SubstitutePersonnelFileId = response.SubstitutePersonnelFileId,
            SubstitutePositionSlotId = response.SubstitutePositionSlotPublicId,
            StartDate = response.StartDate,
            EndDate = response.EndDate,
            Notes = response.Notes,
            IsActive = response.IsActive
        };

    public AuthorizationSubstitutionInput ToInput() =>
        new(
            SubstitutionTypeCode,
            SubstitutePersonnelFileId,
            SubstitutePositionSlotId,
            StartDate,
            EndDate,
            IsActive,
            Notes);
}

