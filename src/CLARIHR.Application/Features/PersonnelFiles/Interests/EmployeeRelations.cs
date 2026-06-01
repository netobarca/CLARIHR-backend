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

public sealed record PersonnelFileEmployeeRelationResponse(
    Guid EmployeeRelationPublicId,
    Guid RelatedEmployeePublicId,
    string RelatedEmployeeFullName,
    string Relationship,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => EmployeeRelationPublicId;
}

public sealed record GetPersonnelFileEmployeeRelationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>;

public sealed record GetPersonnelFileEmployeeRelationByIdQuery(Guid PersonnelFileId, Guid EmployeeRelationPublicId)
    : IQuery<PersonnelFileEmployeeRelationResponse>;

public sealed record AddPersonnelFileEmployeeRelationCommand(
    Guid PersonnelFileId,
    EmployeeRelationInput Relation)
    : ICommand<PersonnelFileEmployeeRelationResponse>;

public sealed record UpdatePersonnelFileEmployeeRelationCommand(
    Guid PersonnelFileId,
    Guid RelationPublicId,
    EmployeeRelationInput Relation,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEmployeeRelationResponse>;

public sealed record DeletePersonnelFileEmployeeRelationCommand(
    Guid PersonnelFileId,
    Guid RelationPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileEmployeeRelationPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileEmployeeRelationCommand(
    Guid PersonnelFileId,
    Guid EmployeeRelationPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileEmployeeRelationPatchOperation> Operations)
    : ICommand<PersonnelFileEmployeeRelationResponse>;

public sealed record EmployeeRelationInput(Guid RelatedEmployeePublicId, string Relationship);

internal sealed class GetPersonnelFileEmployeeRelationsQueryValidator : AbstractValidator<GetPersonnelFileEmployeeRelationsQuery>
{
    public GetPersonnelFileEmployeeRelationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEmployeeRelationByIdQueryValidator : AbstractValidator<GetPersonnelFileEmployeeRelationByIdQuery>
{
    public GetPersonnelFileEmployeeRelationByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EmployeeRelationPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileEmployeeRelationCommandValidator : AbstractValidator<AddPersonnelFileEmployeeRelationCommand>
{
    public AddPersonnelFileEmployeeRelationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Relation).SetValidator(new EmployeeRelationInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEmployeeRelationCommandValidator : AbstractValidator<UpdatePersonnelFileEmployeeRelationCommand>
{
    public UpdatePersonnelFileEmployeeRelationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RelationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Relation).SetValidator(new EmployeeRelationInputValidator());
    }
}

internal sealed class DeletePersonnelFileEmployeeRelationCommandValidator : AbstractValidator<DeletePersonnelFileEmployeeRelationCommand>
{
    public DeletePersonnelFileEmployeeRelationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RelationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileEmployeeRelationCommandValidator : AbstractValidator<PatchPersonnelFileEmployeeRelationCommand>
{
    public PatchPersonnelFileEmployeeRelationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmployeeRelationPublicId).NotEmpty();
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

internal sealed class EmployeeRelationInputValidator : AbstractValidator<EmployeeRelationInput>
{
    public EmployeeRelationInputValidator()
    {
        RuleFor(input => input.RelatedEmployeePublicId).NotEmpty();
        RuleFor(input => input.Relationship).NotEmpty().MaximumLength(80);
    }
}

internal sealed class PersonnelFileEmployeeRelationPatchState
{
    public Guid RelatedEmployeePublicId { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public bool HasMutation { get; set; }

    public static PersonnelFileEmployeeRelationPatchState From(PersonnelFileEmployeeRelationResponse response) =>
        new()
        {
            RelatedEmployeePublicId = response.RelatedEmployeePublicId,
            Relationship = response.Relationship
        };

    public EmployeeRelationInput ToInput() =>
        new(RelatedEmployeePublicId, Relationship);
}

