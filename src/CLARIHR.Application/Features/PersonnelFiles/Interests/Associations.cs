using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;
using FluentValidation.Results;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileAssociationResponse(
    Guid AssociationPublicId,
    string AssociationName,
    string? Role,
    DateTime? JoinedDate,
    DateTime? LeftDate,
    decimal? Payment,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => AssociationPublicId;
}

public sealed record GetPersonnelFileAssociationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileAssociationResponse>>;

public sealed record GetPersonnelFileAssociationByIdQuery(Guid PersonnelFileId, Guid AssociationPublicId)
    : IQuery<PersonnelFileAssociationResponse>;

public sealed record AddPersonnelFileAssociationCommand(
    Guid PersonnelFileId,
    AssociationInput Association)
    : ICommand<PersonnelFileAssociationResponse>;

public sealed record UpdatePersonnelFileAssociationCommand(
    Guid PersonnelFileId,
    Guid AssociationPublicId,
    AssociationInput Association,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAssociationResponse>;

public sealed record DeletePersonnelFileAssociationCommand(
    Guid PersonnelFileId,
    Guid AssociationPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileAssociationPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileAssociationCommand(
    Guid PersonnelFileId,
    Guid AssociationPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileAssociationPatchOperation> Operations)
    : ICommand<PersonnelFileAssociationResponse>;

public sealed record AssociationInput(
    string AssociationName,
    string? Role,
    DateTime? JoinedDate,
    DateTime? LeftDate,
    decimal? Payment);

internal sealed class GetPersonnelFileAssociationsQueryValidator : AbstractValidator<GetPersonnelFileAssociationsQuery>
{
    public GetPersonnelFileAssociationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAssociationByIdQueryValidator : AbstractValidator<GetPersonnelFileAssociationByIdQuery>
{
    public GetPersonnelFileAssociationByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.AssociationPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileAssociationCommandValidator : AbstractValidator<AddPersonnelFileAssociationCommand>
{
    public AddPersonnelFileAssociationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Association).SetValidator(new AssociationInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAssociationCommandValidator : AbstractValidator<UpdatePersonnelFileAssociationCommand>
{
    public UpdatePersonnelFileAssociationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AssociationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Association).SetValidator(new AssociationInputValidator());
    }
}

internal sealed class DeletePersonnelFileAssociationCommandValidator : AbstractValidator<DeletePersonnelFileAssociationCommand>
{
    public DeletePersonnelFileAssociationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AssociationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileAssociationCommandValidator : AbstractValidator<PatchPersonnelFileAssociationCommand>
{
    public PatchPersonnelFileAssociationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AssociationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class AssociationInputValidator : AbstractValidator<AssociationInput>
{
    public AssociationInputValidator()
    {
        RuleFor(input => input.AssociationName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.Role).MaximumLength(120);
        RuleFor(input => input.Payment).GreaterThanOrEqualTo(0).When(static input => input.Payment.HasValue);
        RuleFor(input => input)
            .Must(static input => !input.JoinedDate.HasValue || !input.LeftDate.HasValue || input.LeftDate.Value.Date >= input.JoinedDate.Value.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
    }
}

internal sealed class PersonnelFileAssociationPatchState
{
    public string AssociationName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public DateTime? JoinedDate { get; set; }
    public DateTime? LeftDate { get; set; }
    public decimal? Payment { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileAssociationPatchState From(PersonnelFileAssociationResponse response) =>
        new()
        {
            AssociationName = response.AssociationName,
            Role = response.Role,
            JoinedDate = response.JoinedDate,
            LeftDate = response.LeftDate,
            Payment = response.Payment
        };

    public AssociationInput ToInput() =>
        new(AssociationName, Role, JoinedDate, LeftDate, Payment);
}

