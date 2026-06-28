using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Metadata of a supporting document attached to an economic-aid request (D-06 — evidence of the emergency). The
/// binary lives in the shared file-storage subsystem and is fetched through the request-authorized read-url
/// endpoint. The document-type classification is OPTIONAL.
/// </summary>
public sealed record EconomicAidRequestDocumentResponse(
    Guid Id,
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
    DateTime? ModifiedAtUtc);

public sealed record GetEconomicAidRequestDocumentsQuery(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId) : IQuery<IReadOnlyCollection<EconomicAidRequestDocumentResponse>>;

public sealed record GetEconomicAidRequestDocumentByIdQuery(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    Guid DocumentPublicId) : IQuery<EconomicAidRequestDocumentResponse>;

/// <summary>
/// Request-authorized download. The generic <c>GET /files/{id}/read-url</c> is owner-only, so an attachment's
/// binary is fetched through this endpoint, which authorizes the economic-aid request (same gate as the read
/// query — perm or owner) and mints the read SAS server-side for the document's underlying <c>StoredFile</c>.
/// </summary>
public sealed record GetEconomicAidRequestDocumentReadUrlQuery(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    Guid DocumentPublicId) : IQuery<GetEconomicAidRequestDocumentReadUrlResponse>;

public sealed record GetEconomicAidRequestDocumentReadUrlResponse(
    string ReadUrl,
    DateTime ExpiresUtc);

public sealed record AddEconomicAidRequestDocumentCommand(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations) : ICommand<EconomicAidRequestDocumentResponse>;

public sealed record DeleteEconomicAidRequestDocumentCommand(
    Guid PersonnelFileId,
    Guid EconomicAidRequestPublicId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken) : ICommand<PersonnelFileParentConcurrencyResult>;

internal sealed class GetEconomicAidRequestDocumentsQueryValidator : AbstractValidator<GetEconomicAidRequestDocumentsQuery>
{
    public GetEconomicAidRequestDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EconomicAidRequestPublicId).NotEmpty();
    }
}

internal sealed class GetEconomicAidRequestDocumentByIdQueryValidator : AbstractValidator<GetEconomicAidRequestDocumentByIdQuery>
{
    public GetEconomicAidRequestDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class GetEconomicAidRequestDocumentReadUrlQueryValidator : AbstractValidator<GetEconomicAidRequestDocumentReadUrlQuery>
{
    public GetEconomicAidRequestDocumentReadUrlQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddEconomicAidRequestDocumentCommandValidator : AbstractValidator<AddEconomicAidRequestDocumentCommand>
{
    public AddEconomicAidRequestDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        // DocumentTypeCatalogItemPublicId is OPTIONAL (D-06): no NotEmpty.
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class DeleteEconomicAidRequestDocumentCommandValidator : AbstractValidator<DeleteEconomicAidRequestDocumentCommand>
{
    public DeleteEconomicAidRequestDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EconomicAidRequestPublicId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
