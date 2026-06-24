using CLARIHR.Application.Abstractions.Auditing;
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

internal sealed class GetOffPayrollTransactionDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetOffPayrollTransactionDocumentsQuery, IReadOnlyCollection<OffPayrollTransactionDocumentResponse>>
{
    public async Task<Result<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>>> Handle(
        GetOffPayrollTransactionDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOffPayrollReadAsync<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var transactionInternalId = await employeeRepository.GetOffPayrollTransactionInternalIdAsync(personnelFile!.PublicId, query.OffPayrollTransactionPublicId, cancellationToken);
        if (transactionInternalId is null)
        {
            return Result<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await employeeRepository.GetOffPayrollTransactionDocumentsAsync(query.OffPayrollTransactionPublicId, cancellationToken);
        return Result<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>>.Success(response);
    }
}

internal sealed class GetOffPayrollTransactionDocumentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetOffPayrollTransactionDocumentByIdQuery, OffPayrollTransactionDocumentResponse>
{
    public async Task<Result<OffPayrollTransactionDocumentResponse>> Handle(
        GetOffPayrollTransactionDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOffPayrollReadAsync<OffPayrollTransactionDocumentResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var transactionInternalId = await employeeRepository.GetOffPayrollTransactionInternalIdAsync(personnelFile!.PublicId, query.OffPayrollTransactionPublicId, cancellationToken);
        if (transactionInternalId is null)
        {
            return Result<OffPayrollTransactionDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetOffPayrollTransactionDocumentAsync(query.OffPayrollTransactionPublicId, query.DocumentPublicId, cancellationToken);
        return document is null
            ? Result<OffPayrollTransactionDocumentResponse>.Failure(PersonnelFileErrors.DocumentNotFound)
            : Result<OffPayrollTransactionDocumentResponse>.Success(document);
    }
}

internal sealed class GetOffPayrollTransactionDocumentReadUrlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetOffPayrollTransactionDocumentReadUrlQuery, GetOffPayrollTransactionDocumentReadUrlResponse>
{
    public async Task<Result<GetOffPayrollTransactionDocumentReadUrlResponse>> Handle(
        GetOffPayrollTransactionDocumentReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOffPayrollReadAsync<GetOffPayrollTransactionDocumentReadUrlResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var transactionInternalId = await employeeRepository.GetOffPayrollTransactionInternalIdAsync(personnelFile!.PublicId, query.OffPayrollTransactionPublicId, cancellationToken);
        if (transactionInternalId is null)
        {
            return Result<GetOffPayrollTransactionDocumentReadUrlResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetOffPayrollTransactionDocumentAsync(query.OffPayrollTransactionPublicId, query.DocumentPublicId, cancellationToken);
        if (document is null)
        {
            return Result<GetOffPayrollTransactionDocumentReadUrlResponse>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        var file = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetOffPayrollTransactionDocumentReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetOffPayrollTransactionDocumentReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetOffPayrollTransactionDocumentReadUrlResponse>.Success(
            new GetOffPayrollTransactionDocumentReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}

internal sealed class AddOffPayrollTransactionDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddOffPayrollTransactionDocumentCommand, OffPayrollTransactionDocumentResponse>
{
    public async Task<Result<OffPayrollTransactionDocumentResponse>> Handle(
        AddOffPayrollTransactionDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only (D-06): the dedicated manage permission. No self-service.
        var (failure, personnelFile) = await LoadForManageOffPayrollTransactionsAsync<OffPayrollTransactionDocumentResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OffPayrollTransactionDocumentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var transactionInternalId = await employeeRepository.GetOffPayrollTransactionInternalIdAsync(personnelFile.PublicId, command.OffPayrollTransactionPublicId, cancellationToken);
        if (transactionInternalId is null)
        {
            return Result<OffPayrollTransactionDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<OffPayrollTransactionDocumentResponse>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<OffPayrollTransactionDocumentResponse>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantContext.TenantId!.Value)
        {
            return Result<OffPayrollTransactionDocumentResponse>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.OffPayrollTransactionDocument)
        {
            return Result<OffPayrollTransactionDocumentResponse>.Failure(FileErrors.InvalidPurpose(storedFile.Purpose.ToString()));
        }

        // Document-type classification is optional (D-07). When supplied it must resolve to an active catalog item.
        long? documentTypeInternalId = null;
        if (command.DocumentTypeCatalogItemPublicId is { } documentTypePublicId && documentTypePublicId != Guid.Empty)
        {
            var documentTypeLookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(documentTypePublicId, cancellationToken);
            if (documentTypeLookup is null)
            {
                return Result<OffPayrollTransactionDocumentResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                    }));
            }

            documentTypeInternalId = documentTypeLookup.InternalId;
        }

        var entity = OffPayrollTransactionDocument.Create(
            Guid.NewGuid(),
            documentTypeInternalId,
            storedFile.PublicId,
            storedFile.FileName,
            storedFile.ContentType,
            (int)storedFile.SizeBytes,
            command.Observations);
        entity.BindToOffPayrollTransaction(transactionInternalId.Value);
        entity.SetTenantId(personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AddOffPayrollTransactionDocumentAsync(entity, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await employeeRepository.GetOffPayrollTransactionDocumentAsync(command.OffPayrollTransactionPublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Off-payroll transaction document could not be resolved after upload.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Attached document {response.FileName} to an off-payroll transaction of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OffPayrollTransactionDocumentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteOffPayrollTransactionDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteOffPayrollTransactionDocumentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeleteOffPayrollTransactionDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOffPayrollTransactionsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var transactionInternalId = await employeeRepository.GetOffPayrollTransactionInternalIdAsync(personnelFile.PublicId, command.OffPayrollTransactionPublicId, cancellationToken);
        if (transactionInternalId is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetOffPayrollTransactionDocumentEntityAsync(
            command.OffPayrollTransactionPublicId, command.DocumentPublicId, personnelFile.TenantId, cancellationToken);
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
            // Soft-delete the document (retention) and soft-delete the backing stored file so the
            // orphan-cleanup job removes the blob.
            document.Inactivate();
            var storedFile = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
            storedFile?.MarkDeleted();
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Deleted document {document.FileName} from an off-payroll transaction of {personnelFile.FullName}.", null, cancellationToken);
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
