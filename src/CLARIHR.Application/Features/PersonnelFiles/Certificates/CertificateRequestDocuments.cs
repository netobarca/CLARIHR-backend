using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Metadata of a certificate's document (D-05). <see cref="IsSystemGenerated"/> is true for the PDF the system
/// generated on issuance, false for a manual override uploaded by HR. The binary is fetched through the
/// request-authorized read-url endpoint.
/// </summary>
public sealed record CertificateRequestDocumentResponse(
    Guid Id,
    bool IsSystemGenerated,
    string? Observations,
    Guid FilePublicId,
    string FileName,
    string ContentType,
    int SizeBytes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record GetCertificateRequestDocumentsQuery(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId) : IQuery<IReadOnlyCollection<CertificateRequestDocumentResponse>>;

public sealed record GetCertificateRequestDocumentByIdQuery(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    Guid DocumentPublicId) : IQuery<CertificateRequestDocumentResponse>;

/// <summary>
/// Request-authorized download of a certificate document (the issued PDF or a manual override). Authorizes the
/// certificate request (same gate as the read query — permission or owner) and mints a read SAS server-side for
/// the document's underlying <c>StoredFile</c>.
/// </summary>
public sealed record GetCertificateRequestDocumentReadUrlQuery(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    Guid DocumentPublicId) : IQuery<GetCertificateRequestDocumentReadUrlResponse>;

public sealed record GetCertificateRequestDocumentReadUrlResponse(
    string ReadUrl,
    DateTime ExpiresUtc);

/// <summary>Attaches a manual override document (HR-only, D-05): links an already-uploaded file. IsSystemGenerated = false.</summary>
public sealed record AddCertificateRequestDocumentCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    Guid FilePublicId,
    string? Observations) : ICommand<CertificateRequestDocumentResponse>;

public sealed record DeleteCertificateRequestDocumentCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken) : ICommand<PersonnelFileParentConcurrencyResult>;

internal sealed class GetCertificateRequestDocumentsQueryValidator : AbstractValidator<GetCertificateRequestDocumentsQuery>
{
    public GetCertificateRequestDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CertificateRequestPublicId).NotEmpty();
    }
}

internal sealed class GetCertificateRequestDocumentByIdQueryValidator : AbstractValidator<GetCertificateRequestDocumentByIdQuery>
{
    public GetCertificateRequestDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CertificateRequestPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class GetCertificateRequestDocumentReadUrlQueryValidator : AbstractValidator<GetCertificateRequestDocumentReadUrlQuery>
{
    public GetCertificateRequestDocumentReadUrlQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CertificateRequestPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddCertificateRequestDocumentCommandValidator : AbstractValidator<AddCertificateRequestDocumentCommand>
{
    public AddCertificateRequestDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.CertificateRequestPublicId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class DeleteCertificateRequestDocumentCommandValidator : AbstractValidator<DeleteCertificateRequestDocumentCommand>
{
    public DeleteCertificateRequestDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.CertificateRequestPublicId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
