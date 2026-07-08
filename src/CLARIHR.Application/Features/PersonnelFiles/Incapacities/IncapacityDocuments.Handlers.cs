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
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class GetIncapacityDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetIncapacityDocumentsQuery, IReadOnlyCollection<IncapacityDocumentResponse>>
{
    public async Task<Result<IReadOnlyCollection<IncapacityDocumentResponse>>> Handle(
        GetIncapacityDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForIncapacityReadAsync<IReadOnlyCollection<IncapacityDocumentResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var incapacityInternalId = await incapacityRepository.GetInternalIdAsync(personnelFile!.PublicId, query.IncapacityPublicId, cancellationToken);
        if (incapacityInternalId is null)
        {
            return Result<IReadOnlyCollection<IncapacityDocumentResponse>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await incapacityRepository.GetDocumentResponsesAsync(query.IncapacityPublicId, cancellationToken);
        return Result<IReadOnlyCollection<IncapacityDocumentResponse>>.Success(response);
    }
}

internal sealed class GetIncapacityDocumentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetIncapacityDocumentByIdQuery, IncapacityDocumentResponse>
{
    public async Task<Result<IncapacityDocumentResponse>> Handle(
        GetIncapacityDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForIncapacityReadAsync<IncapacityDocumentResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var incapacityInternalId = await incapacityRepository.GetInternalIdAsync(personnelFile!.PublicId, query.IncapacityPublicId, cancellationToken);
        if (incapacityInternalId is null)
        {
            return Result<IncapacityDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await incapacityRepository.GetDocumentResponseAsync(query.IncapacityPublicId, query.DocumentPublicId, cancellationToken);
        return document is null
            ? Result<IncapacityDocumentResponse>.Failure(PersonnelFileErrors.DocumentNotFound)
            : Result<IncapacityDocumentResponse>.Success(document);
    }
}

internal sealed class GetIncapacityDocumentReadUrlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetIncapacityDocumentReadUrlQuery, GetIncapacityDocumentReadUrlResponse>
{
    public async Task<Result<GetIncapacityDocumentReadUrlResponse>> Handle(
        GetIncapacityDocumentReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForIncapacityReadAsync<GetIncapacityDocumentReadUrlResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var incapacityInternalId = await incapacityRepository.GetInternalIdAsync(personnelFile!.PublicId, query.IncapacityPublicId, cancellationToken);
        if (incapacityInternalId is null)
        {
            return Result<GetIncapacityDocumentReadUrlResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await incapacityRepository.GetDocumentResponseAsync(query.IncapacityPublicId, query.DocumentPublicId, cancellationToken);
        if (document is null)
        {
            return Result<GetIncapacityDocumentReadUrlResponse>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        var file = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetIncapacityDocumentReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetIncapacityDocumentReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetIncapacityDocumentReadUrlResponse>.Success(
            new GetIncapacityDocumentReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}

internal sealed class AddIncapacityDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddIncapacityDocumentCommand, IncapacityDocumentResponse>
{
    public async Task<Result<IncapacityDocumentResponse>> Handle(
        AddIncapacityDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Self-service create (D-18): the manage permission OR the employee attaching to their own incapacity.
        var (failure, personnelFile, _) = await LoadForCreateOwnOrManageIncapacityAsync<IncapacityDocumentResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<IncapacityDocumentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var incapacityInternalId = await incapacityRepository.GetInternalIdAsync(personnelFile.PublicId, command.IncapacityPublicId, cancellationToken);
        if (incapacityInternalId is null)
        {
            return Result<IncapacityDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<IncapacityDocumentResponse>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<IncapacityDocumentResponse>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantContext.TenantId!.Value)
        {
            return Result<IncapacityDocumentResponse>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.IncapacityDocument)
        {
            return Result<IncapacityDocumentResponse>.Failure(IncapacityErrors.DocumentPurposeInvalid);
        }

        long? documentTypeInternalId = null;
        if (command.DocumentTypeCatalogItemPublicId is { } documentTypePublicId)
        {
            var lookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(documentTypePublicId, cancellationToken);
            if (lookup is null)
            {
                return Result<IncapacityDocumentResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                    }));
            }

            documentTypeInternalId = lookup.InternalId;
        }

        var entity = PersonnelFileIncapacityDocument.Create(
            Guid.NewGuid(),
            documentTypeInternalId,
            storedFile.PublicId,
            storedFile.FileName,
            storedFile.ContentType,
            (int)storedFile.SizeBytes,
            command.Observations);
        entity.BindToIncapacity(incapacityInternalId.Value);
        entity.SetTenantId(personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            incapacityRepository.AddDocument(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await incapacityRepository.GetDocumentResponseAsync(command.IncapacityPublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity document could not be resolved after upload.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Attached document {response.FileName} to an incapacity of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<IncapacityDocumentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteIncapacityDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteIncapacityDocumentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeleteIncapacityDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Deletes are manager-only.
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var incapacityInternalId = await incapacityRepository.GetInternalIdAsync(personnelFile!.PublicId, command.IncapacityPublicId, cancellationToken);
        if (incapacityInternalId is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await incapacityRepository.GetDocumentEntityAsync(
            command.IncapacityPublicId, command.DocumentPublicId, personnelFile.TenantId, cancellationToken);
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
                auditService, personnelFile, $"Deleted document {document.FileName} from an incapacity of {personnelFile.FullName}.", null, cancellationToken);
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
