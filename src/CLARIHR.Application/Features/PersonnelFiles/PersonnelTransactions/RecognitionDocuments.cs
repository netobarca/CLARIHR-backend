using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>Supporting document (diploma / memo) attached to a recognition (REQ-003 D-12/RF-005).</summary>
public sealed record RecognitionDocumentResponse(
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

/// <summary>Time-limited pre-signed download URL for a recognition document's binary.</summary>
public sealed record GetRecognitionDocumentReadUrlResponse(string ReadUrl, DateTime ExpiresUtc);

public sealed record GetRecognitionDocumentsQuery(Guid PersonnelFileId, Guid RecognitionPublicId)
    : IQuery<IReadOnlyCollection<RecognitionDocumentResponse>>;

public sealed record GetRecognitionDocumentByIdQuery(Guid PersonnelFileId, Guid RecognitionPublicId, Guid DocumentPublicId)
    : IQuery<RecognitionDocumentResponse>;

public sealed record GetRecognitionDocumentReadUrlQuery(Guid PersonnelFileId, Guid RecognitionPublicId, Guid DocumentPublicId)
    : IQuery<GetRecognitionDocumentReadUrlResponse>;

public sealed record AddRecognitionDocumentCommand(
    Guid PersonnelFileId,
    Guid RecognitionPublicId,
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations)
    : ICommand<RecognitionDocumentResponse>;

public sealed record DeleteRecognitionDocumentCommand(
    Guid PersonnelFileId,
    Guid RecognitionPublicId,
    Guid DocumentPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

// ── Validators ───────────────────────────────────────────────────────────────────────────────────

internal sealed class GetRecognitionDocumentsQueryValidator : AbstractValidator<GetRecognitionDocumentsQuery>
{
    public GetRecognitionDocumentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecognitionPublicId).NotEmpty();
    }
}

internal sealed class GetRecognitionDocumentByIdQueryValidator : AbstractValidator<GetRecognitionDocumentByIdQuery>
{
    public GetRecognitionDocumentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecognitionPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class GetRecognitionDocumentReadUrlQueryValidator : AbstractValidator<GetRecognitionDocumentReadUrlQuery>
{
    public GetRecognitionDocumentReadUrlQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.RecognitionPublicId).NotEmpty();
        RuleFor(query => query.DocumentPublicId).NotEmpty();
    }
}

internal sealed class AddRecognitionDocumentCommandValidator : AbstractValidator<AddRecognitionDocumentCommand>
{
    public AddRecognitionDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecognitionPublicId).NotEmpty();
        RuleFor(command => command.FilePublicId).NotEmpty();
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class DeleteRecognitionDocumentCommandValidator : AbstractValidator<DeleteRecognitionDocumentCommand>
{
    public DeleteRecognitionDocumentCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.RecognitionPublicId).NotEmpty();
        RuleFor(command => command.DocumentPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
