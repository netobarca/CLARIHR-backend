using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Metadata of a document ("comprobante") attached to an off-payroll transaction (D-07). Mirrors
/// <see cref="MedicalClaimDocumentResponse"/>; the binary lives in the shared file-storage subsystem and is
/// fetched through the transaction-authorized read-url endpoint. The document-type classification is OPTIONAL.
/// </summary>
public sealed record OffPayrollTransactionDocumentResponse(
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

public sealed record GetOffPayrollTransactionDocumentsQuery(
    Guid PersonnelFileId,
    Guid OffPayrollTransactionPublicId) : IQuery<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>>;

public sealed record GetOffPayrollTransactionDocumentByIdQuery(
    Guid PersonnelFileId,
    Guid OffPayrollTransactionPublicId,
    Guid DocumentPublicId) : IQuery<OffPayrollTransactionDocumentResponse>;

/// <summary>
/// Transaction-authorized download. The generic <c>GET /files/{id}/read-url</c> is owner-only, so an
/// attachment's binary is fetched through this endpoint, which authorizes the off-payroll transaction (same gate
/// as the read query) and mints the read SAS server-side for the document's underlying <c>StoredFile</c>.
/// </summary>
public sealed record GetOffPayrollTransactionDocumentReadUrlQuery(
    Guid PersonnelFileId,
    Guid OffPayrollTransactionPublicId,
    Guid DocumentPublicId) : IQuery<GetOffPayrollTransactionDocumentReadUrlResponse>;

public sealed record GetOffPayrollTransactionDocumentReadUrlResponse(
    string ReadUrl,
    DateTime ExpiresUtc);

public sealed record AddOffPayrollTransactionDocumentCommand(
    Guid PersonnelFileId,
    Guid OffPayrollTransactionPublicId,
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations) : ICommand<OffPayrollTransactionDocumentResponse>;

public sealed record DeleteOffPayrollTransactionDocumentCommand(
    Guid PersonnelFileId,
    Guid OffPayrollTransactionPublicId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken) : ICommand<PersonnelFileParentConcurrencyResult>;

internal sealed class GetOffPayrollTransactionDocumentsQueryValidator : AbstractValidator<GetOffPayrollTransactionDocumentsQuery>
{
    public GetOffPayrollTransactionDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OffPayrollTransactionPublicId).NotEmpty();
    }
}

internal sealed class GetOffPayrollTransactionDocumentByIdQueryValidator : AbstractValidator<GetOffPayrollTransactionDocumentByIdQuery>
{
    public GetOffPayrollTransactionDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OffPayrollTransactionPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class GetOffPayrollTransactionDocumentReadUrlQueryValidator : AbstractValidator<GetOffPayrollTransactionDocumentReadUrlQuery>
{
    public GetOffPayrollTransactionDocumentReadUrlQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OffPayrollTransactionPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddOffPayrollTransactionDocumentCommandValidator : AbstractValidator<AddOffPayrollTransactionDocumentCommand>
{
    public AddOffPayrollTransactionDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OffPayrollTransactionPublicId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        // DocumentTypeCatalogItemPublicId is OPTIONAL (D-07 — "comprobante de cualquier índole"): no NotEmpty.
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class DeleteOffPayrollTransactionDocumentCommandValidator : AbstractValidator<DeleteOffPayrollTransactionDocumentCommand>
{
    public DeleteOffPayrollTransactionDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.OffPayrollTransactionPublicId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
