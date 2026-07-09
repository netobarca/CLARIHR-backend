using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>Shared read-gate glue for recognition documents: authorize the recognition (View OR self-APLICADA).</summary>
internal static class RecognitionDocumentReadSupport
{
    /// <summary>
    /// True when the caller may NOT see this recognition's documents — the self-service employee only ever
    /// reaches their APLICADA recognitions (D-13). A non-applied record is masked as not found.
    /// </summary>
    public static bool IsHidden(bool restrictToApplied, PersonnelFileRecognitionResponse? recognition) =>
        recognition is null || (restrictToApplied && recognition.StatusCode != PersonnelTransactionStatuses.Aplicada);
}

internal sealed class GetRecognitionDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetRecognitionDocumentsQuery, IReadOnlyCollection<RecognitionDocumentResponse>>
{
    public async Task<Result<IReadOnlyCollection<RecognitionDocumentResponse>>> Handle(
        GetRecognitionDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForRecognitionReadAsync<IReadOnlyCollection<RecognitionDocumentResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var recognition = await transactionRepository.GetRecognitionResponseAsync(personnelFile!.PublicId, query.RecognitionPublicId, cancellationToken);
        if (RecognitionDocumentReadSupport.IsHidden(restrictToApplied, recognition))
        {
            return Result<IReadOnlyCollection<RecognitionDocumentResponse>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await transactionRepository.GetRecognitionDocumentsAsync(query.RecognitionPublicId, cancellationToken);
        return Result<IReadOnlyCollection<RecognitionDocumentResponse>>.Success(response);
    }
}

internal sealed class GetRecognitionDocumentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetRecognitionDocumentByIdQuery, RecognitionDocumentResponse>
{
    public async Task<Result<RecognitionDocumentResponse>> Handle(
        GetRecognitionDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForRecognitionReadAsync<RecognitionDocumentResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var recognition = await transactionRepository.GetRecognitionResponseAsync(personnelFile!.PublicId, query.RecognitionPublicId, cancellationToken);
        if (RecognitionDocumentReadSupport.IsHidden(restrictToApplied, recognition))
        {
            return Result<RecognitionDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await transactionRepository.GetRecognitionDocumentAsync(query.RecognitionPublicId, query.DocumentPublicId, cancellationToken);
        return document is null
            ? Result<RecognitionDocumentResponse>.Failure(PersonnelFileErrors.DocumentNotFound)
            : Result<RecognitionDocumentResponse>.Success(document);
    }
}

internal sealed class GetRecognitionDocumentReadUrlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetRecognitionDocumentReadUrlQuery, GetRecognitionDocumentReadUrlResponse>
{
    public async Task<Result<GetRecognitionDocumentReadUrlResponse>> Handle(
        GetRecognitionDocumentReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForRecognitionReadAsync<GetRecognitionDocumentReadUrlResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var recognition = await transactionRepository.GetRecognitionResponseAsync(personnelFile!.PublicId, query.RecognitionPublicId, cancellationToken);
        if (RecognitionDocumentReadSupport.IsHidden(restrictToApplied, recognition))
        {
            return Result<GetRecognitionDocumentReadUrlResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await transactionRepository.GetRecognitionDocumentAsync(query.RecognitionPublicId, query.DocumentPublicId, cancellationToken);
        if (document is null)
        {
            return Result<GetRecognitionDocumentReadUrlResponse>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        var file = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetRecognitionDocumentReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetRecognitionDocumentReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetRecognitionDocumentReadUrlResponse>.Success(
            new GetRecognitionDocumentReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}

internal sealed class AddRecognitionDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddRecognitionDocumentCommand, RecognitionDocumentResponse>
{
    public async Task<Result<RecognitionDocumentResponse>> Handle(
        AddRecognitionDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Attaching documents is manager-only (D-05), like every recognition write.
        var (failure, personnelFile) = await LoadForManageRecognitionsAsync<RecognitionDocumentResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecognitionDocumentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var recognitionInternalId = await transactionRepository.GetRecognitionInternalIdAsync(personnelFile.PublicId, command.RecognitionPublicId, cancellationToken);
        if (recognitionInternalId is null)
        {
            return Result<RecognitionDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<RecognitionDocumentResponse>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<RecognitionDocumentResponse>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantContext.TenantId!.Value)
        {
            return Result<RecognitionDocumentResponse>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.RecognitionDocument)
        {
            return Result<RecognitionDocumentResponse>.Failure(FileErrors.InvalidPurpose(storedFile.Purpose.ToString()));
        }

        long? documentTypeInternalId = null;
        if (command.DocumentTypeCatalogItemPublicId is { } documentTypePublicId)
        {
            var lookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(documentTypePublicId, cancellationToken);
            if (lookup is null)
            {
                return Result<RecognitionDocumentResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                    }));
            }

            documentTypeInternalId = lookup.InternalId;
        }

        var entity = PersonnelFileRecognitionDocument.Create(
            Guid.NewGuid(),
            documentTypeInternalId,
            storedFile.PublicId,
            storedFile.FileName,
            storedFile.ContentType,
            (int)storedFile.SizeBytes,
            command.Observations);
        entity.BindToRecognition(recognitionInternalId.Value);
        entity.SetTenantId(personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            transactionRepository.AddRecognitionDocument(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetRecognitionDocumentAsync(command.RecognitionPublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition document could not be resolved after upload.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Attached document {response.FileName} to a recognition of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RecognitionDocumentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteRecognitionDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteRecognitionDocumentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeleteRecognitionDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecognitionsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var recognitionInternalId = await transactionRepository.GetRecognitionInternalIdAsync(personnelFile.PublicId, command.RecognitionPublicId, cancellationToken);
        if (recognitionInternalId is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await transactionRepository.GetRecognitionDocumentEntityAsync(
            command.RecognitionPublicId, command.DocumentPublicId, personnelFile.TenantId, cancellationToken);
        if (document is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        if (document.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            document.Inactivate();
            var storedFile = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
            storedFile?.MarkDeleted();
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Deleted document {document.FileName} from a recognition of {personnelFile.FullName}.", null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileParentConcurrencyResult>.Success(
                new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
