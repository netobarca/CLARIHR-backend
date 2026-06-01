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

public sealed record PersonnelFileEmergencyContactResponse(
    Guid EmergencyContactPublicId,
    string Name,
    string Relationship,
    string Phone,
    string? Address,
    string? Workplace,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => EmergencyContactPublicId;
}

public sealed record GetPersonnelFileEmergencyContactsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>;

public sealed record GetPersonnelFileEmergencyContactByIdQuery(Guid PersonnelFileId, Guid EmergencyContactPublicId)
    : IQuery<PersonnelFileEmergencyContactResponse>;

public sealed record AddPersonnelFileEmergencyContactCommand(
    Guid PersonnelFileId,
    EmergencyContactInput EmergencyContact)
    : ICommand<PersonnelFileEmergencyContactResponse>;

public sealed record UpdatePersonnelFileEmergencyContactCommand(
    Guid PersonnelFileId,
    Guid EmergencyContactPublicId,
    EmergencyContactInput EmergencyContact,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEmergencyContactResponse>;

public sealed record DeletePersonnelFileEmergencyContactCommand(
    Guid PersonnelFileId,
    Guid EmergencyContactPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileEmergencyContactPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileEmergencyContactCommand(
    Guid PersonnelFileId,
    Guid EmergencyContactPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileEmergencyContactPatchOperation> Operations)
    : ICommand<PersonnelFileEmergencyContactResponse>;

public sealed record EmergencyContactInput(
    string Name,
    string Relationship,
    string Phone,
    string? Address,
    string? Workplace);

internal sealed class GetPersonnelFileEmergencyContactsQueryValidator : AbstractValidator<GetPersonnelFileEmergencyContactsQuery>
{
    public GetPersonnelFileEmergencyContactsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEmergencyContactByIdQueryValidator : AbstractValidator<GetPersonnelFileEmergencyContactByIdQuery>
{
    public GetPersonnelFileEmergencyContactByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EmergencyContactPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileEmergencyContactCommandValidator : AbstractValidator<AddPersonnelFileEmergencyContactCommand>
{
    public AddPersonnelFileEmergencyContactCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmergencyContact).SetValidator(new EmergencyContactInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEmergencyContactCommandValidator : AbstractValidator<UpdatePersonnelFileEmergencyContactCommand>
{
    public UpdatePersonnelFileEmergencyContactCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmergencyContactPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.EmergencyContact).SetValidator(new EmergencyContactInputValidator());
    }
}

internal sealed class DeletePersonnelFileEmergencyContactCommandValidator : AbstractValidator<DeletePersonnelFileEmergencyContactCommand>
{
    public DeletePersonnelFileEmergencyContactCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmergencyContactPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileEmergencyContactCommandValidator : AbstractValidator<PatchPersonnelFileEmergencyContactCommand>
{
    public PatchPersonnelFileEmergencyContactCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmergencyContactPublicId).NotEmpty();
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

internal sealed class EmergencyContactInputValidator : AbstractValidator<EmergencyContactInput>
{
    public EmergencyContactInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().MaximumLength(150);
        RuleFor(input => input.Relationship).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Phone)
            .NotEmpty()
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .WithMessage("Phone format is invalid.");
        RuleFor(input => input.Address).MaximumLength(500);
        RuleFor(input => input.Workplace).MaximumLength(200);
    }
}

internal sealed class PersonnelFileEmergencyContactPatchState
{
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Workplace { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileEmergencyContactPatchState From(PersonnelFileEmergencyContactResponse response) =>
        new()
        {
            Name = response.Name,
            Relationship = response.Relationship,
            Phone = response.Phone,
            Address = response.Address,
            Workplace = response.Workplace
        };

    public EmergencyContactInput ToInput() =>
        new(
            Name,
            Relationship,
            Phone,
            Address,
            Workplace);
}

