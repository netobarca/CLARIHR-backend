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

public sealed record PersonnelFileDocumentMetadataResponse(
    Guid Id,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentTypeCode,
    string? DocumentTypeName,
    string DocumentType,
    string? Observations,
    Guid FilePublicId,
    string FileName,
    string ContentType,
    int SizeBytes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record GetPersonnelFileDocumentsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>;

public sealed record GetPersonnelFileDocumentByIdQuery(
    Guid PersonnelFileId,
    Guid DocumentPublicId) : IQuery<PersonnelFileDocumentMetadataResponse>;

// FILE-1 (security): domain-delegated download. The generic GET /files/{id}/read-url is now
// owner-only, so a document binary is fetched through this personnel-file-authorized endpoint
// instead. Authorizes the personnel file (same gate as GetPersonnelFileDocumentByIdQuery), then
// mints the read SAS server-side for the document's underlying StoredFile.
public sealed record GetPersonnelFileDocumentReadUrlQuery(
    Guid PersonnelFileId,
    Guid DocumentPublicId) : IQuery<GetPersonnelFileDocumentReadUrlResponse>;

public sealed record GetPersonnelFileDocumentReadUrlResponse(
    string ReadUrl,
    DateTime ExpiresUtc);

public sealed record UpdatePersonnelFileDocumentCommand(
    Guid PersonnelFileId,
    Guid DocumentPublicId,
    Guid DocumentTypeCatalogItemPublicId,
    string? Observations,
    // null = only update metadata; present = replace file reference
    Guid? FilePublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileDocumentMetadataResponse>;

public sealed record PersonnelFileDocumentPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileDocumentCommand(
    Guid PersonnelFileId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileDocumentPatchOperation> Operations)
    : ICommand<PersonnelFileDocumentMetadataResponse>;

public sealed record DeletePersonnelFileDocumentCommand(
    Guid PersonnelFileId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record AddPersonnelFileDocumentCommand(
    Guid PersonnelFileId,
    Guid FilePublicId,
    Guid DocumentTypeCatalogItemPublicId,
    string? Observations)
    : ICommand<PersonnelFileDocumentMetadataResponse>;

internal sealed class GetPersonnelFileDocumentsQueryValidator : AbstractValidator<GetPersonnelFileDocumentsQuery>
{
    public GetPersonnelFileDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileDocumentByIdQueryValidator : AbstractValidator<GetPersonnelFileDocumentByIdQuery>
{
    public GetPersonnelFileDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileDocumentCommandValidator : AbstractValidator<AddPersonnelFileDocumentCommand>
{
    public AddPersonnelFileDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        RuleFor(command => command.DocumentTypeCatalogItemPublicId).NotEmpty();
    }
}

internal sealed class UpdatePersonnelFileDocumentCommandValidator : AbstractValidator<UpdatePersonnelFileDocumentCommand>
{
    public UpdatePersonnelFileDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.DocumentTypeCatalogItemPublicId).NotEmpty();
        RuleFor(command => command.Observations).MaximumLength(1000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileDocumentCommandValidator : AbstractValidator<PatchPersonnelFileDocumentCommand>
{
    public PatchPersonnelFileDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
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

internal sealed class DeletePersonnelFileDocumentCommandValidator : AbstractValidator<DeletePersonnelFileDocumentCommand>
{
    public DeletePersonnelFileDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PersonnelFileDocumentPatchState
{
    public Guid DocumentTypeCatalogItemPublicId { get; set; }
    public string? Observations { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileDocumentPatchState From(PersonnelFileDocumentMetadataResponse response) =>
        new()
        {
            DocumentTypeCatalogItemPublicId = response.DocumentTypeCatalogItemPublicId ?? Guid.Empty,
            Observations = response.Observations
        };
}

