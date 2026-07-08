using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Metadata of a supporting document ("constancia") attached to an incapacity (D-22/RF-011). Mirrors
/// <see cref="MedicalClaimDocumentResponse"/>; the binary lives in the shared file-storage subsystem and is
/// fetched through the incapacity-authorized read-url endpoint. The document-type classification is optional.
/// </summary>
public sealed record IncapacityDocumentResponse(
    Guid IncapacityDocumentPublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentTypeCode,
    string? DocumentTypeName,
    string? Observations,
    Guid FilePublicId,
    string FileName,
    string ContentType,
    int SizeBytes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc)
{
    [JsonIgnore]
    public Guid Id => IncapacityDocumentPublicId;
}

public sealed record GetIncapacityDocumentsQuery(
    Guid PersonnelFileId,
    Guid IncapacityPublicId) : IQuery<IReadOnlyCollection<IncapacityDocumentResponse>>;

public sealed record GetIncapacityDocumentByIdQuery(
    Guid PersonnelFileId,
    Guid IncapacityPublicId,
    Guid DocumentPublicId) : IQuery<IncapacityDocumentResponse>;

/// <summary>
/// Incapacity-authorized download. The generic <c>GET /files/{id}/read-url</c> is owner-only, so an
/// attachment's binary is fetched through this endpoint, which authorizes the incapacity (same gate as the
/// read query) and mints the read SAS server-side for the document's underlying <c>StoredFile</c>.
/// </summary>
public sealed record GetIncapacityDocumentReadUrlQuery(
    Guid PersonnelFileId,
    Guid IncapacityPublicId,
    Guid DocumentPublicId) : IQuery<GetIncapacityDocumentReadUrlResponse>;

public sealed record GetIncapacityDocumentReadUrlResponse(
    string ReadUrl,
    DateTime ExpiresUtc);

public sealed record AddIncapacityDocumentCommand(
    Guid PersonnelFileId,
    Guid IncapacityPublicId,
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations) : ICommand<IncapacityDocumentResponse>;

public sealed record DeleteIncapacityDocumentCommand(
    Guid PersonnelFileId,
    Guid IncapacityPublicId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken) : ICommand<PersonnelFileParentConcurrencyResult>;

internal sealed class GetIncapacityDocumentsQueryValidator : AbstractValidator<GetIncapacityDocumentsQuery>
{
    public GetIncapacityDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.IncapacityPublicId).NotEmpty();
    }
}

internal sealed class GetIncapacityDocumentByIdQueryValidator : AbstractValidator<GetIncapacityDocumentByIdQuery>
{
    public GetIncapacityDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.IncapacityPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class GetIncapacityDocumentReadUrlQueryValidator : AbstractValidator<GetIncapacityDocumentReadUrlQuery>
{
    public GetIncapacityDocumentReadUrlQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.IncapacityPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddIncapacityDocumentCommandValidator : AbstractValidator<AddIncapacityDocumentCommand>
{
    public AddIncapacityDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IncapacityPublicId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class DeleteIncapacityDocumentCommandValidator : AbstractValidator<DeleteIncapacityDocumentCommand>
{
    public DeleteIncapacityDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.IncapacityPublicId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
