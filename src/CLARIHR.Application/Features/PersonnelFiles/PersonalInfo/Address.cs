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

public sealed record PersonnelFileAddressResponse(
    Guid AddressPublicId,
    string AddressLine,
    string? AddressTypeCode,
    string? Country,
    string? Department,
    string? Municipality,
    string? PostalCode,
    bool IsCurrent,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => AddressPublicId;
}

public sealed record GetPersonnelFileAddressesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileAddressResponse>>;

public sealed record GetPersonnelFileAddressByIdQuery(Guid PersonnelFileId, Guid AddressPublicId)
    : IQuery<PersonnelFileAddressResponse>;

public sealed record AddPersonnelFileAddressCommand(
    Guid PersonnelFileId,
    AddressInput Address)
    : ICommand<PersonnelFileAddressResponse>;

public sealed record UpdatePersonnelFileAddressCommand(
    Guid PersonnelFileId,
    Guid AddressPublicId,
    AddressInput Address,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAddressResponse>;

public sealed record DeletePersonnelFileAddressCommand(
    Guid PersonnelFileId,
    Guid AddressPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileAddressPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileAddressCommand(
    Guid PersonnelFileId,
    Guid AddressPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileAddressPatchOperation> Operations)
    : ICommand<PersonnelFileAddressResponse>;

public sealed record AddressInput(
    string AddressLine,
    string? AddressTypeCode,
    string? Country,
    string? Department,
    string? Municipality,
    string? PostalCode,
    bool IsCurrent = false);

internal sealed class GetPersonnelFileAddressesQueryValidator : AbstractValidator<GetPersonnelFileAddressesQuery>
{
    public GetPersonnelFileAddressesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAddressByIdQueryValidator : AbstractValidator<GetPersonnelFileAddressByIdQuery>
{
    public GetPersonnelFileAddressByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.AddressPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileAddressCommandValidator : AbstractValidator<AddPersonnelFileAddressCommand>
{
    public AddPersonnelFileAddressCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Address).SetValidator(new AddressInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAddressCommandValidator : AbstractValidator<UpdatePersonnelFileAddressCommand>
{
    public UpdatePersonnelFileAddressCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AddressPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Address).SetValidator(new AddressInputValidator());
    }
}

internal sealed class DeletePersonnelFileAddressCommandValidator : AbstractValidator<DeletePersonnelFileAddressCommand>
{
    public DeletePersonnelFileAddressCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AddressPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileAddressCommandValidator : AbstractValidator<PatchPersonnelFileAddressCommand>
{
    public PatchPersonnelFileAddressCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.AddressPublicId).NotEmpty();
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

internal sealed class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(input => input.AddressLine).NotEmpty().MaximumLength(500);
        RuleFor(input => input.AddressTypeCode)
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode!)
            .When(input => !string.IsNullOrWhiteSpace(input.AddressTypeCode))
            .WithMessage("AddressTypeCode format is invalid.");
        RuleFor(input => input.Country).MaximumLength(120);
        RuleFor(input => input.Department).MaximumLength(120);
        RuleFor(input => input.Municipality).MaximumLength(120);
        RuleFor(input => input.PostalCode).MaximumLength(40);
    }
}

internal sealed class PersonnelFileAddressPatchState
{
    public string AddressLine { get; set; } = string.Empty;
    public string? AddressTypeCode { get; set; }
    public string? Country { get; set; }
    public string? Department { get; set; }
    public string? Municipality { get; set; }
    public string? PostalCode { get; set; }
    public bool IsCurrent { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileAddressPatchState From(PersonnelFileAddressResponse response) =>
        new()
        {
            AddressLine = response.AddressLine,
            AddressTypeCode = response.AddressTypeCode,
            Country = response.Country,
            Department = response.Department,
            Municipality = response.Municipality,
            PostalCode = response.PostalCode,
            IsCurrent = response.IsCurrent
        };

    public AddressInput ToInput() =>
        new(
            AddressLine,
            AddressTypeCode,
            Country,
            Department,
            Municipality,
            PostalCode,
            IsCurrent);
}

