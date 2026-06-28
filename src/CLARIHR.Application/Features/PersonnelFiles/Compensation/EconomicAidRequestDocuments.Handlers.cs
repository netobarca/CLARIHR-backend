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

internal sealed class GetEconomicAidRequestDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetEconomicAidRequestDocumentsQuery, IReadOnlyCollection<EconomicAidRequestDocumentResponse>>
{
    public async Task<Result<IReadOnlyCollection<EconomicAidRequestDocumentResponse>>> Handle(
        GetEconomicAidRequestDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForEconomicAidReadAsync<IReadOnlyCollection<EconomicAidRequestDocumentResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var requestInternalId = await employeeRepository.GetEconomicAidRequestInternalIdAsync(personnelFile!.PublicId, query.EconomicAidRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<IReadOnlyCollection<EconomicAidRequestDocumentResponse>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await employeeRepository.GetEconomicAidRequestDocumentsAsync(query.EconomicAidRequestPublicId, cancellationToken);
        return Result<IReadOnlyCollection<EconomicAidRequestDocumentResponse>>.Success(response);
    }
}

internal sealed class GetEconomicAidRequestDocumentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetEconomicAidRequestDocumentByIdQuery, EconomicAidRequestDocumentResponse>
{
    public async Task<Result<EconomicAidRequestDocumentResponse>> Handle(
        GetEconomicAidRequestDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForEconomicAidReadAsync<EconomicAidRequestDocumentResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var requestInternalId = await employeeRepository.GetEconomicAidRequestInternalIdAsync(personnelFile!.PublicId, query.EconomicAidRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<EconomicAidRequestDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetEconomicAidRequestDocumentAsync(query.EconomicAidRequestPublicId, query.DocumentPublicId, cancellationToken);
        return document is null
            ? Result<EconomicAidRequestDocumentResponse>.Failure(PersonnelFileErrors.DocumentNotFound)
            : Result<EconomicAidRequestDocumentResponse>.Success(document);
    }
}

internal sealed class GetEconomicAidRequestDocumentReadUrlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetEconomicAidRequestDocumentReadUrlQuery, GetEconomicAidRequestDocumentReadUrlResponse>
{
    public async Task<Result<GetEconomicAidRequestDocumentReadUrlResponse>> Handle(
        GetEconomicAidRequestDocumentReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForEconomicAidReadAsync<GetEconomicAidRequestDocumentReadUrlResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var requestInternalId = await employeeRepository.GetEconomicAidRequestInternalIdAsync(personnelFile!.PublicId, query.EconomicAidRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<GetEconomicAidRequestDocumentReadUrlResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetEconomicAidRequestDocumentAsync(query.EconomicAidRequestPublicId, query.DocumentPublicId, cancellationToken);
        if (document is null)
        {
            return Result<GetEconomicAidRequestDocumentReadUrlResponse>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        var file = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetEconomicAidRequestDocumentReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetEconomicAidRequestDocumentReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetEconomicAidRequestDocumentReadUrlResponse>.Success(
            new GetEconomicAidRequestDocumentReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}

internal sealed class AddEconomicAidRequestDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddEconomicAidRequestDocumentCommand, EconomicAidRequestDocumentResponse>
{
    public async Task<Result<EconomicAidRequestDocumentResponse>> Handle(
        AddEconomicAidRequestDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Self-service (D-06): the employee may attach evidence to their OWN request, or HR to any.
        var (failure, personnelFile) = await LoadForCreateOwnOrManageEconomicAidAsync<EconomicAidRequestDocumentResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<EconomicAidRequestDocumentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var requestInternalId = await employeeRepository.GetEconomicAidRequestInternalIdAsync(personnelFile.PublicId, command.EconomicAidRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<EconomicAidRequestDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<EconomicAidRequestDocumentResponse>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<EconomicAidRequestDocumentResponse>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantContext.TenantId!.Value)
        {
            return Result<EconomicAidRequestDocumentResponse>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.EconomicAidRequestDocument)
        {
            return Result<EconomicAidRequestDocumentResponse>.Failure(FileErrors.InvalidPurpose(storedFile.Purpose.ToString()));
        }

        // Document-type classification is optional (D-06). When supplied it must resolve to an active catalog item.
        long? documentTypeInternalId = null;
        if (command.DocumentTypeCatalogItemPublicId is { } documentTypePublicId && documentTypePublicId != Guid.Empty)
        {
            var documentTypeLookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(documentTypePublicId, cancellationToken);
            if (documentTypeLookup is null)
            {
                return Result<EconomicAidRequestDocumentResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                    }));
            }

            documentTypeInternalId = documentTypeLookup.InternalId;
        }

        var entity = EconomicAidRequestDocument.Create(
            Guid.NewGuid(),
            documentTypeInternalId,
            storedFile.PublicId,
            storedFile.FileName,
            storedFile.ContentType,
            (int)storedFile.SizeBytes,
            command.Observations);
        entity.BindToEconomicAidRequest(requestInternalId.Value);
        entity.SetTenantId(personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AddEconomicAidRequestDocumentAsync(entity, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await employeeRepository.GetEconomicAidRequestDocumentAsync(command.EconomicAidRequestPublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Economic-aid request document could not be resolved after upload.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Attached document {response.FileName} to an economic-aid request of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<EconomicAidRequestDocumentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteEconomicAidRequestDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteEconomicAidRequestDocumentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeleteEconomicAidRequestDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only: removing an attachment is a manage operation.
        var (failure, personnelFile) = await LoadForManageEconomicAidRequestsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var requestInternalId = await employeeRepository.GetEconomicAidRequestInternalIdAsync(personnelFile.PublicId, command.EconomicAidRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetEconomicAidRequestDocumentEntityAsync(
            command.EconomicAidRequestPublicId, command.DocumentPublicId, personnelFile.TenantId, cancellationToken);
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
            // Soft-delete the document (retention) and mark the backing stored file for orphan cleanup.
            document.Inactivate();
            var storedFile = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
            storedFile?.MarkDeleted();
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Deleted document {document.FileName} from an economic-aid request of {personnelFile.FullName}.", null, cancellationToken);
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
