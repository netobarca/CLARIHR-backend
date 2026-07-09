using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>Supporting document (acta / descargo) attached to a disciplinary action (REQ-003 D-12/RF-008).</summary>
public sealed record DisciplinaryActionDocumentResponse(
    Guid DocumentPublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentTypeName,
    Guid FilePublicId,
    string FileName,
    string ContentType,
    int SizeBytes,
    string? Observations,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => DocumentPublicId;
}

/// <summary>Time-limited pre-signed download URL for a disciplinary-action document's binary.</summary>
public sealed record GetDisciplinaryActionDocumentReadUrlResponse(string ReadUrl, DateTime ExpiresUtc);

public sealed record GetDisciplinaryActionDocumentsQuery(Guid PersonnelFileId, Guid DisciplinaryActionPublicId)
    : IQuery<IReadOnlyCollection<DisciplinaryActionDocumentResponse>>;

public sealed record GetDisciplinaryActionDocumentByIdQuery(Guid PersonnelFileId, Guid DisciplinaryActionPublicId, Guid DocumentPublicId)
    : IQuery<DisciplinaryActionDocumentResponse>;

public sealed record GetDisciplinaryActionDocumentReadUrlQuery(Guid PersonnelFileId, Guid DisciplinaryActionPublicId, Guid DocumentPublicId)
    : IQuery<GetDisciplinaryActionDocumentReadUrlResponse>;

public sealed record AddDisciplinaryActionDocumentCommand(
    Guid PersonnelFileId,
    Guid DisciplinaryActionPublicId,
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations)
    : ICommand<DisciplinaryActionDocumentResponse>;

public sealed record DeleteDisciplinaryActionDocumentCommand(
    Guid PersonnelFileId,
    Guid DisciplinaryActionPublicId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

// ── Validators ───────────────────────────────────────────────────────────────────────────────────

internal sealed class GetDisciplinaryActionDocumentsQueryValidator : AbstractValidator<GetDisciplinaryActionDocumentsQuery>
{
    public GetDisciplinaryActionDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.DisciplinaryActionPublicId).NotEmpty();
    }
}

internal sealed class GetDisciplinaryActionDocumentByIdQueryValidator : AbstractValidator<GetDisciplinaryActionDocumentByIdQuery>
{
    public GetDisciplinaryActionDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.DisciplinaryActionPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class GetDisciplinaryActionDocumentReadUrlQueryValidator : AbstractValidator<GetDisciplinaryActionDocumentReadUrlQuery>
{
    public GetDisciplinaryActionDocumentReadUrlQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.DisciplinaryActionPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddDisciplinaryActionDocumentCommandValidator : AbstractValidator<AddDisciplinaryActionDocumentCommand>
{
    public AddDisciplinaryActionDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DisciplinaryActionPublicId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class DeleteDisciplinaryActionDocumentCommandValidator : AbstractValidator<DeleteDisciplinaryActionDocumentCommand>
{
    public DeleteDisciplinaryActionDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.DisciplinaryActionPublicId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
