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

public sealed record PersonnelFileLanguageResponse(
    Guid LanguagePublicId,
    string LanguageCode,
    string LevelCode,
    bool Speaks,
    bool Writes,
    bool Reads,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => LanguagePublicId;
}

public sealed record GetPersonnelFileLanguagesQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileLanguageResponse>>;

public sealed record GetPersonnelFileLanguageByIdQuery(Guid PersonnelFileId, Guid LanguagePublicId)
    : IQuery<PersonnelFileLanguageResponse>;

public sealed record AddPersonnelFileLanguageCommand(
    Guid PersonnelFileId,
    LanguageInput Language)
    : ICommand<PersonnelFileLanguageResponse>;

public sealed record UpdatePersonnelFileLanguageCommand(
    Guid PersonnelFileId,
    Guid LanguagePublicId,
    LanguageInput Language,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileLanguageResponse>;

public sealed record DeletePersonnelFileLanguageCommand(
    Guid PersonnelFileId,
    Guid LanguagePublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileLanguagePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileLanguageCommand(
    Guid PersonnelFileId,
    Guid LanguagePublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileLanguagePatchOperation> Operations)
    : ICommand<PersonnelFileLanguageResponse>;

public sealed record LanguageInput(
    string LanguageCode,
    string LevelCode,
    bool Speaks,
    bool Writes,
    bool Reads);

internal sealed class GetPersonnelFileLanguagesQueryValidator : AbstractValidator<GetPersonnelFileLanguagesQuery>
{
    public GetPersonnelFileLanguagesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileLanguageByIdQueryValidator : AbstractValidator<GetPersonnelFileLanguageByIdQuery>
{
    public GetPersonnelFileLanguageByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.LanguagePublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileLanguageCommandValidator : AbstractValidator<AddPersonnelFileLanguageCommand>
{
    public AddPersonnelFileLanguageCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Language).SetValidator(new LanguageInputValidator());
    }
}

internal sealed class UpdatePersonnelFileLanguageCommandValidator : AbstractValidator<UpdatePersonnelFileLanguageCommand>
{
    public UpdatePersonnelFileLanguageCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.LanguagePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Language).SetValidator(new LanguageInputValidator());
    }
}

internal sealed class DeletePersonnelFileLanguageCommandValidator : AbstractValidator<DeletePersonnelFileLanguageCommand>
{
    public DeletePersonnelFileLanguageCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.LanguagePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileLanguageCommandValidator : AbstractValidator<PatchPersonnelFileLanguageCommand>
{
    public PatchPersonnelFileLanguageCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.LanguagePublicId).NotEmpty();
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

internal sealed class LanguageInputValidator : AbstractValidator<LanguageInput>
{
    public LanguageInputValidator()
    {
        RuleFor(input => input.LanguageCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.LevelCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input)
            .Must(static input => input.Speaks || input.Writes || input.Reads)
            .WithMessage("At least one of Speaks, Writes, or Reads must be true.");
    }
}

internal sealed class PersonnelFileLanguagePatchState
{
    public string LanguageCode { get; set; } = string.Empty;
    public string LevelCode { get; set; } = string.Empty;
    public bool Speaks { get; set; }
    public bool Writes { get; set; }
    public bool Reads { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileLanguagePatchState From(PersonnelFileLanguageResponse response) =>
        new()
        {
            LanguageCode = response.LanguageCode,
            LevelCode = response.LevelCode,
            Speaks = response.Speaks,
            Writes = response.Writes,
            Reads = response.Reads
        };

    public LanguageInput ToInput() =>
        new(LanguageCode, LevelCode, Speaks, Writes, Reads);
}

