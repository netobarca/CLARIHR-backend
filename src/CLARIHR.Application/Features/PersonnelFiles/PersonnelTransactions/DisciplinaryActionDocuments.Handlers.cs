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

/// <summary>Shared read-gate glue for disciplinary-action documents: authorize the record (View OR self-APLICADA).</summary>
internal static class DisciplinaryActionDocumentReadSupport
{
    /// <summary>
    /// True when the caller may NOT see this disciplinary action's documents — the self-service employee only
    /// ever reaches their APLICADA disciplinary actions (D-13). A non-applied record is masked as not found.
    /// </summary>
    public static bool IsHidden(bool restrictToApplied, PersonnelFileDisciplinaryActionResponse? disciplinaryAction) =>
        disciplinaryAction is null || (restrictToApplied && disciplinaryAction.StatusCode != PersonnelTransactionStatuses.Aplicada);
}

internal sealed class GetDisciplinaryActionDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetDisciplinaryActionDocumentsQuery, IReadOnlyCollection<DisciplinaryActionDocumentResponse>>
{
    public async Task<Result<IReadOnlyCollection<DisciplinaryActionDocumentResponse>>> Handle(
        GetDisciplinaryActionDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForDisciplinaryActionReadAsync<IReadOnlyCollection<DisciplinaryActionDocumentResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var disciplinaryAction = await transactionRepository.GetDisciplinaryActionResponseAsync(personnelFile!.PublicId, query.DisciplinaryActionPublicId, cancellationToken);
        if (DisciplinaryActionDocumentReadSupport.IsHidden(restrictToApplied, disciplinaryAction))
        {
            return Result<IReadOnlyCollection<DisciplinaryActionDocumentResponse>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await transactionRepository.GetDisciplinaryActionDocumentsAsync(query.DisciplinaryActionPublicId, cancellationToken);
        return Result<IReadOnlyCollection<DisciplinaryActionDocumentResponse>>.Success(response);
    }
}

internal sealed class GetDisciplinaryActionDocumentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetDisciplinaryActionDocumentByIdQuery, DisciplinaryActionDocumentResponse>
{
    public async Task<Result<DisciplinaryActionDocumentResponse>> Handle(
        GetDisciplinaryActionDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForDisciplinaryActionReadAsync<DisciplinaryActionDocumentResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var disciplinaryAction = await transactionRepository.GetDisciplinaryActionResponseAsync(personnelFile!.PublicId, query.DisciplinaryActionPublicId, cancellationToken);
        if (DisciplinaryActionDocumentReadSupport.IsHidden(restrictToApplied, disciplinaryAction))
        {
            return Result<DisciplinaryActionDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await transactionRepository.GetDisciplinaryActionDocumentAsync(query.DisciplinaryActionPublicId, query.DocumentPublicId, cancellationToken);
        return document is null
            ? Result<DisciplinaryActionDocumentResponse>.Failure(PersonnelFileErrors.DocumentNotFound)
            : Result<DisciplinaryActionDocumentResponse>.Success(document);
    }
}

internal sealed class GetDisciplinaryActionDocumentReadUrlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetDisciplinaryActionDocumentReadUrlQuery, GetDisciplinaryActionDocumentReadUrlResponse>
{
    public async Task<Result<GetDisciplinaryActionDocumentReadUrlResponse>> Handle(
        GetDisciplinaryActionDocumentReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForDisciplinaryActionReadAsync<GetDisciplinaryActionDocumentReadUrlResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var disciplinaryAction = await transactionRepository.GetDisciplinaryActionResponseAsync(personnelFile!.PublicId, query.DisciplinaryActionPublicId, cancellationToken);
        if (DisciplinaryActionDocumentReadSupport.IsHidden(restrictToApplied, disciplinaryAction))
        {
            return Result<GetDisciplinaryActionDocumentReadUrlResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await transactionRepository.GetDisciplinaryActionDocumentAsync(query.DisciplinaryActionPublicId, query.DocumentPublicId, cancellationToken);
        if (document is null)
        {
            return Result<GetDisciplinaryActionDocumentReadUrlResponse>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        var file = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetDisciplinaryActionDocumentReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetDisciplinaryActionDocumentReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetDisciplinaryActionDocumentReadUrlResponse>.Success(
            new GetDisciplinaryActionDocumentReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}

internal sealed class AddDisciplinaryActionDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddDisciplinaryActionDocumentCommand, DisciplinaryActionDocumentResponse>
{
    public async Task<Result<DisciplinaryActionDocumentResponse>> Handle(
        AddDisciplinaryActionDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Attaching documents is manager-only (D-05), like every disciplinary-action write.
        var (failure, personnelFile) = await LoadForManageDisciplinaryActionsAsync<DisciplinaryActionDocumentResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<DisciplinaryActionDocumentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var disciplinaryActionInternalId = await transactionRepository.GetDisciplinaryActionInternalIdAsync(personnelFile.PublicId, command.DisciplinaryActionPublicId, cancellationToken);
        if (disciplinaryActionInternalId is null)
        {
            return Result<DisciplinaryActionDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<DisciplinaryActionDocumentResponse>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<DisciplinaryActionDocumentResponse>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantContext.TenantId!.Value)
        {
            return Result<DisciplinaryActionDocumentResponse>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.DisciplinaryActionDocument)
        {
            return Result<DisciplinaryActionDocumentResponse>.Failure(FileErrors.InvalidPurpose(storedFile.Purpose.ToString()));
        }

        long? documentTypeInternalId = null;
        if (command.DocumentTypeCatalogItemPublicId is { } documentTypePublicId)
        {
            var lookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(documentTypePublicId, cancellationToken);
            if (lookup is null)
            {
                return Result<DisciplinaryActionDocumentResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                    }));
            }

            documentTypeInternalId = lookup.InternalId;
        }

        var entity = PersonnelFileDisciplinaryActionDocument.Create(
            Guid.NewGuid(),
            documentTypeInternalId,
            storedFile.PublicId,
            storedFile.FileName,
            storedFile.ContentType,
            (int)storedFile.SizeBytes,
            command.Observations);
        entity.BindToDisciplinaryAction(disciplinaryActionInternalId.Value);
        entity.SetTenantId(personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            transactionRepository.AddDisciplinaryActionDocument(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetDisciplinaryActionDocumentAsync(command.DisciplinaryActionPublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action document could not be resolved after upload.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Attached document {response.FileName} to a disciplinary action of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<DisciplinaryActionDocumentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteDisciplinaryActionDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteDisciplinaryActionDocumentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeleteDisciplinaryActionDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageDisciplinaryActionsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var disciplinaryActionInternalId = await transactionRepository.GetDisciplinaryActionInternalIdAsync(personnelFile.PublicId, command.DisciplinaryActionPublicId, cancellationToken);
        if (disciplinaryActionInternalId is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await transactionRepository.GetDisciplinaryActionDocumentEntityAsync(
            command.DisciplinaryActionPublicId, command.DocumentPublicId, personnelFile.TenantId, cancellationToken);
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
                auditService, personnelFile, $"Deleted document {document.FileName} from a disciplinary action of {personnelFile.FullName}.", null, cancellationToken);
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
