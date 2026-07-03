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

public sealed record PersonnelFileHobbyResponse(Guid HobbyPublicId, string HobbyCode, string? HobbyName, Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => HobbyPublicId;
}

public sealed record GetPersonnelFileHobbiesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileHobbyResponse>>;

public sealed record GetPersonnelFileHobbyByIdQuery(Guid PersonnelFileId, Guid HobbyPublicId)
    : IQuery<PersonnelFileHobbyResponse>;

public sealed record AddPersonnelFileHobbyCommand(
    Guid PersonnelFileId,
    HobbyInput Hobby)
    : ICommand<PersonnelFileHobbyResponse>;

public sealed record UpdatePersonnelFileHobbyCommand(
    Guid PersonnelFileId,
    Guid HobbyPublicId,
    HobbyInput Hobby,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileHobbyResponse>;

public sealed record DeletePersonnelFileHobbyCommand(
    Guid PersonnelFileId,
    Guid HobbyPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileHobbyPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileHobbyCommand(
    Guid PersonnelFileId,
    Guid HobbyPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileHobbyPatchOperation> Operations)
    : ICommand<PersonnelFileHobbyResponse>;

public sealed record HobbyInput(string HobbyCode, string? HobbyName = null);

internal sealed class GetPersonnelFileHobbiesQueryValidator : AbstractValidator<GetPersonnelFileHobbiesQuery>
{
    public GetPersonnelFileHobbiesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileHobbyByIdQueryValidator : AbstractValidator<GetPersonnelFileHobbyByIdQuery>
{
    public GetPersonnelFileHobbyByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.HobbyPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileHobbyCommandValidator : AbstractValidator<AddPersonnelFileHobbyCommand>
{
    public AddPersonnelFileHobbyCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Hobby).SetValidator(new HobbyInputValidator());
    }
}

internal sealed class UpdatePersonnelFileHobbyCommandValidator : AbstractValidator<UpdatePersonnelFileHobbyCommand>
{
    public UpdatePersonnelFileHobbyCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.HobbyPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Hobby).SetValidator(new HobbyInputValidator());
    }
}

internal sealed class DeletePersonnelFileHobbyCommandValidator : AbstractValidator<DeletePersonnelFileHobbyCommand>
{
    public DeletePersonnelFileHobbyCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.HobbyPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileHobbyCommandValidator : AbstractValidator<PatchPersonnelFileHobbyCommand>
{
    public PatchPersonnelFileHobbyCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.HobbyPublicId).NotEmpty();
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

internal sealed class HobbyInputValidator : AbstractValidator<HobbyInput>
{
    public HobbyInputValidator()
    {
        RuleFor(input => input.HobbyCode)
            .NotEmpty()
            .MaximumLength(80)
            .Must(PersonnelFileValidationRules.IsValidCode)
            .WithMessage("HobbyCode format is invalid.");
        RuleFor(input => input.HobbyName).MaximumLength(120);
    }
}

internal sealed class PersonnelFileHobbyPatchState
{
    public string HobbyCode { get; set; } = string.Empty;
    public string? HobbyName { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileHobbyPatchState From(PersonnelFileHobbyResponse response) =>
        new()
        {
            HobbyCode = response.HobbyCode,
            HobbyName = response.HobbyName
        };

    public HobbyInput ToInput() =>
        new(HobbyCode, HobbyName);
}

