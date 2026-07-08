using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Metadata of an authorization document attached to a compensatory-time credit (D-20/RF-012). Mirrors
/// <see cref="IncapacityDocumentResponse"/>; the binary lives in the shared file-storage subsystem and is
/// fetched through the credit-authorized read-url endpoint. The document-type classification is optional.
/// </summary>
public sealed record CompensatoryTimeCreditDocumentResponse(
    Guid CompensatoryTimeCreditDocumentPublicId,
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
    public Guid Id => CompensatoryTimeCreditDocumentPublicId;
}

public sealed record GetCompensatoryTimeCreditDocumentsQuery(
    Guid PersonnelFileId,
    Guid CompensatoryTimeCreditPublicId) : IQuery<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>;

public sealed record GetCompensatoryTimeCreditDocumentByIdQuery(
    Guid PersonnelFileId,
    Guid CompensatoryTimeCreditPublicId,
    Guid DocumentPublicId) : IQuery<CompensatoryTimeCreditDocumentResponse>;

/// <summary>
/// Credit-authorized download. The generic <c>GET /files/{id}/read-url</c> is owner-only, so an attachment's
/// binary is fetched through this endpoint, which authorizes the credit (same gate as the read query) and mints
/// the read SAS server-side for the document's underlying <c>StoredFile</c>.
/// </summary>
public sealed record GetCompensatoryTimeCreditDocumentReadUrlQuery(
    Guid PersonnelFileId,
    Guid CompensatoryTimeCreditPublicId,
    Guid DocumentPublicId) : IQuery<GetCompensatoryTimeCreditDocumentReadUrlResponse>;

public sealed record GetCompensatoryTimeCreditDocumentReadUrlResponse(
    string ReadUrl,
    DateTime ExpiresUtc);

public sealed record AddCompensatoryTimeCreditDocumentCommand(
    Guid PersonnelFileId,
    Guid CompensatoryTimeCreditPublicId,
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations) : ICommand<CompensatoryTimeCreditDocumentResponse>;

public sealed record DeleteCompensatoryTimeCreditDocumentCommand(
    Guid PersonnelFileId,
    Guid CompensatoryTimeCreditPublicId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken) : ICommand<PersonnelFileParentConcurrencyResult>;

internal sealed class GetCompensatoryTimeCreditDocumentsQueryValidator : AbstractValidator<GetCompensatoryTimeCreditDocumentsQuery>
{
    public GetCompensatoryTimeCreditDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CompensatoryTimeCreditPublicId).NotEmpty();
    }
}

internal sealed class GetCompensatoryTimeCreditDocumentByIdQueryValidator : AbstractValidator<GetCompensatoryTimeCreditDocumentByIdQuery>
{
    public GetCompensatoryTimeCreditDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CompensatoryTimeCreditPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class GetCompensatoryTimeCreditDocumentReadUrlQueryValidator : AbstractValidator<GetCompensatoryTimeCreditDocumentReadUrlQuery>
{
    public GetCompensatoryTimeCreditDocumentReadUrlQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CompensatoryTimeCreditPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddCompensatoryTimeCreditDocumentCommandValidator : AbstractValidator<AddCompensatoryTimeCreditDocumentCommand>
{
    public AddCompensatoryTimeCreditDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.CompensatoryTimeCreditPublicId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class DeleteCompensatoryTimeCreditDocumentCommandValidator : AbstractValidator<DeleteCompensatoryTimeCreditDocumentCommand>
{
    public DeleteCompensatoryTimeCreditDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.CompensatoryTimeCreditPublicId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
