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

public sealed record PersonnelReferenceValueResponse(
    string Code,
    string Name);

public sealed record PersonnelFileReferenceResponse(
    Guid ReferencePublicId,
    string PersonName,
    string? Address,
    string Phone,
    string ReferenceTypeCode,
    string? Occupation,
    string? Workplace,
    string? WorkPhone,
    decimal KnownTimeYears,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => ReferencePublicId;
}

public sealed record GetPersonnelFileReferencesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileReferenceResponse>>;

public sealed record GetPersonnelFileReferenceByIdQuery(Guid PersonnelFileId, Guid ReferencePublicId)
    : IQuery<PersonnelFileReferenceResponse>;

public sealed record AddPersonnelFileReferenceCommand(
    Guid PersonnelFileId,
    ReferenceInput Reference)
    : ICommand<PersonnelFileReferenceResponse>;

public sealed record UpdatePersonnelFileReferenceCommand(
    Guid PersonnelFileId,
    Guid ReferencePublicId,
    ReferenceInput Reference,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileReferenceResponse>;

public sealed record DeletePersonnelFileReferenceCommand(
    Guid PersonnelFileId,
    Guid ReferencePublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileReferencePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileReferenceCommand(
    Guid PersonnelFileId,
    Guid ReferencePublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileReferencePatchOperation> Operations)
    : ICommand<PersonnelFileReferenceResponse>;

public sealed record ReferenceInput(
    string PersonName,
    string? Address,
    string Phone,
    string ReferenceTypeCode,
    string? Occupation,
    string? Workplace,
    string? WorkPhone,
    decimal KnownTimeYears);

// PersonnelFileDocumentInput removed — bulk-replace replaced by atomic UpdatePersonnelFileDocumentCommand.

internal sealed class GetPersonnelFileReferencesQueryValidator : AbstractValidator<GetPersonnelFileReferencesQuery>
{
    public GetPersonnelFileReferencesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileReferenceByIdQueryValidator : AbstractValidator<GetPersonnelFileReferenceByIdQuery>
{
    public GetPersonnelFileReferenceByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.ReferencePublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileReferenceCommandValidator : AbstractValidator<AddPersonnelFileReferenceCommand>
{
    public AddPersonnelFileReferenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Reference).SetValidator(new ReferenceInputValidator());
    }
}

internal sealed class UpdatePersonnelFileReferenceCommandValidator : AbstractValidator<UpdatePersonnelFileReferenceCommand>
{
    public UpdatePersonnelFileReferenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ReferencePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reference).SetValidator(new ReferenceInputValidator());
    }
}

internal sealed class DeletePersonnelFileReferenceCommandValidator : AbstractValidator<DeletePersonnelFileReferenceCommand>
{
    public DeletePersonnelFileReferenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ReferencePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileReferenceCommandValidator : AbstractValidator<PatchPersonnelFileReferenceCommand>
{
    public PatchPersonnelFileReferenceCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ReferencePublicId).NotEmpty();
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

internal sealed class ReferenceInputValidator : AbstractValidator<ReferenceInput>
{
    public ReferenceInputValidator()
    {
        RuleFor(input => input.PersonName).NotEmpty().MaximumLength(150);
        RuleFor(input => input.Address).MaximumLength(500);
        RuleFor(input => input.Phone)
            .NotEmpty()
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .WithMessage("Phone format is invalid.");
        RuleFor(input => input.ReferenceTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Occupation).MaximumLength(120);
        RuleFor(input => input.Workplace).MaximumLength(200);
        RuleFor(input => input.WorkPhone)
            .MaximumLength(40)
            .Must(PersonnelFileValidationRules.IsValidPhone)
            .When(input => !string.IsNullOrWhiteSpace(input.WorkPhone))
            .WithMessage("WorkPhone format is invalid.");
        RuleFor(input => input.KnownTimeYears).GreaterThanOrEqualTo(0);
    }
}

internal sealed class PersonnelFileReferencePatchState
{
    public string PersonName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string ReferenceTypeCode { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? Workplace { get; set; }
    public string? WorkPhone { get; set; }
    public decimal KnownTimeYears { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileReferencePatchState From(PersonnelFileReferenceResponse response) =>
        new()
        {
            PersonName = response.PersonName,
            Address = response.Address,
            Phone = response.Phone,
            ReferenceTypeCode = response.ReferenceTypeCode,
            Occupation = response.Occupation,
            Workplace = response.Workplace,
            WorkPhone = response.WorkPhone,
            KnownTimeYears = response.KnownTimeYears
        };

    public ReferenceInput ToInput() =>
        new(
            PersonName,
            Address,
            Phone,
            ReferenceTypeCode,
            Occupation,
            Workplace,
            WorkPhone,
            KnownTimeYears);
}

