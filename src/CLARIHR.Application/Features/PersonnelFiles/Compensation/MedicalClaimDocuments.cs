using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Metadata of a document attached to a medical claim (decision D-11 / RF-012). Mirrors
/// <see cref="PersonnelFileDocumentMetadataResponse"/>; the binary lives in the shared file-storage
/// subsystem and is fetched through the claim-authorized read-url endpoint.
/// </summary>
public sealed record MedicalClaimDocumentResponse(
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

public sealed record GetMedicalClaimDocumentsQuery(
    Guid PersonnelFileId,
    Guid MedicalClaimPublicId) : IQuery<IReadOnlyCollection<MedicalClaimDocumentResponse>>;

public sealed record GetMedicalClaimDocumentByIdQuery(
    Guid PersonnelFileId,
    Guid MedicalClaimPublicId,
    Guid DocumentPublicId) : IQuery<MedicalClaimDocumentResponse>;

/// <summary>
/// Claim-authorized download. The generic <c>GET /files/{id}/read-url</c> is owner-only, so an attachment's
/// binary is fetched through this endpoint, which authorizes the medical claim (same gate as the read query)
/// and mints the read SAS server-side for the document's underlying <c>StoredFile</c>.
/// </summary>
public sealed record GetMedicalClaimDocumentReadUrlQuery(
    Guid PersonnelFileId,
    Guid MedicalClaimPublicId,
    Guid DocumentPublicId) : IQuery<GetMedicalClaimDocumentReadUrlResponse>;

public sealed record GetMedicalClaimDocumentReadUrlResponse(
    string ReadUrl,
    DateTime ExpiresUtc);

public sealed record AddMedicalClaimDocumentCommand(
    Guid PersonnelFileId,
    Guid MedicalClaimPublicId,
    Guid FilePublicId,
    Guid DocumentTypeCatalogItemPublicId,
    string? Observations) : ICommand<MedicalClaimDocumentResponse>;

public sealed record DeleteMedicalClaimDocumentCommand(
    Guid PersonnelFileId,
    Guid MedicalClaimPublicId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken) : ICommand<PersonnelFileParentConcurrencyResult>;

internal sealed class GetMedicalClaimDocumentsQueryValidator : AbstractValidator<GetMedicalClaimDocumentsQuery>
{
    public GetMedicalClaimDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.MedicalClaimPublicId).NotEmpty();
    }
}

internal sealed class GetMedicalClaimDocumentByIdQueryValidator : AbstractValidator<GetMedicalClaimDocumentByIdQuery>
{
    public GetMedicalClaimDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.MedicalClaimPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class GetMedicalClaimDocumentReadUrlQueryValidator : AbstractValidator<GetMedicalClaimDocumentReadUrlQuery>
{
    public GetMedicalClaimDocumentReadUrlQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.MedicalClaimPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddMedicalClaimDocumentCommandValidator : AbstractValidator<AddMedicalClaimDocumentCommand>
{
    public AddMedicalClaimDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.MedicalClaimPublicId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        RuleFor(command => command.DocumentTypeCatalogItemPublicId).NotEmpty();
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class DeleteMedicalClaimDocumentCommandValidator : AbstractValidator<DeleteMedicalClaimDocumentCommand>
{
    public DeleteMedicalClaimDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.MedicalClaimPublicId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
