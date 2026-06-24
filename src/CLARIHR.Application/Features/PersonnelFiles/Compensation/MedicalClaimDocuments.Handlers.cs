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

internal sealed class GetMedicalClaimDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetMedicalClaimDocumentsQuery, IReadOnlyCollection<MedicalClaimDocumentResponse>>
{
    public async Task<Result<IReadOnlyCollection<MedicalClaimDocumentResponse>>> Handle(
        GetMedicalClaimDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForMedicalClaimReadAsync<IReadOnlyCollection<MedicalClaimDocumentResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var claimInternalId = await employeeRepository.GetMedicalClaimInternalIdAsync(personnelFile!.PublicId, query.MedicalClaimPublicId, cancellationToken);
        if (claimInternalId is null)
        {
            return Result<IReadOnlyCollection<MedicalClaimDocumentResponse>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await employeeRepository.GetMedicalClaimDocumentsAsync(query.MedicalClaimPublicId, cancellationToken);
        return Result<IReadOnlyCollection<MedicalClaimDocumentResponse>>.Success(response);
    }
}

internal sealed class GetMedicalClaimDocumentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetMedicalClaimDocumentByIdQuery, MedicalClaimDocumentResponse>
{
    public async Task<Result<MedicalClaimDocumentResponse>> Handle(
        GetMedicalClaimDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForMedicalClaimReadAsync<MedicalClaimDocumentResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var claimInternalId = await employeeRepository.GetMedicalClaimInternalIdAsync(personnelFile!.PublicId, query.MedicalClaimPublicId, cancellationToken);
        if (claimInternalId is null)
        {
            return Result<MedicalClaimDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetMedicalClaimDocumentAsync(query.MedicalClaimPublicId, query.DocumentPublicId, cancellationToken);
        return document is null
            ? Result<MedicalClaimDocumentResponse>.Failure(PersonnelFileErrors.DocumentNotFound)
            : Result<MedicalClaimDocumentResponse>.Success(document);
    }
}

internal sealed class GetMedicalClaimDocumentReadUrlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetMedicalClaimDocumentReadUrlQuery, GetMedicalClaimDocumentReadUrlResponse>
{
    public async Task<Result<GetMedicalClaimDocumentReadUrlResponse>> Handle(
        GetMedicalClaimDocumentReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForMedicalClaimReadAsync<GetMedicalClaimDocumentReadUrlResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var claimInternalId = await employeeRepository.GetMedicalClaimInternalIdAsync(personnelFile!.PublicId, query.MedicalClaimPublicId, cancellationToken);
        if (claimInternalId is null)
        {
            return Result<GetMedicalClaimDocumentReadUrlResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetMedicalClaimDocumentAsync(query.MedicalClaimPublicId, query.DocumentPublicId, cancellationToken);
        if (document is null)
        {
            return Result<GetMedicalClaimDocumentReadUrlResponse>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        var file = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetMedicalClaimDocumentReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetMedicalClaimDocumentReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetMedicalClaimDocumentReadUrlResponse>.Success(
            new GetMedicalClaimDocumentReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}

internal sealed class AddMedicalClaimDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddMedicalClaimDocumentCommand, MedicalClaimDocumentResponse>
{
    public async Task<Result<MedicalClaimDocumentResponse>> Handle(
        AddMedicalClaimDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Self-service create (D-09): the manage permission OR the employee attaching to their own claim.
        var (failure, personnelFile) = await LoadForCreateOwnOrManageMedicalClaimAsync<MedicalClaimDocumentResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<MedicalClaimDocumentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var claimInternalId = await employeeRepository.GetMedicalClaimInternalIdAsync(personnelFile.PublicId, command.MedicalClaimPublicId, cancellationToken);
        if (claimInternalId is null)
        {
            return Result<MedicalClaimDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<MedicalClaimDocumentResponse>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<MedicalClaimDocumentResponse>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantContext.TenantId!.Value)
        {
            return Result<MedicalClaimDocumentResponse>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.MedicalClaimDocument)
        {
            return Result<MedicalClaimDocumentResponse>.Failure(FileErrors.InvalidPurpose(storedFile.Purpose.ToString()));
        }

        var documentTypeLookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(
            command.DocumentTypeCatalogItemPublicId, cancellationToken);
        if (documentTypeLookup is null)
        {
            return Result<MedicalClaimDocumentResponse>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                }));
        }

        var entity = MedicalClaimDocument.Create(
            Guid.NewGuid(),
            documentTypeLookup.InternalId,
            storedFile.PublicId,
            storedFile.FileName,
            storedFile.ContentType,
            (int)storedFile.SizeBytes,
            command.Observations);
        entity.BindToMedicalClaim(claimInternalId.Value);
        entity.SetTenantId(personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AddMedicalClaimDocumentAsync(entity, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await employeeRepository.GetMedicalClaimDocumentAsync(command.MedicalClaimPublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Medical claim document could not be resolved after upload.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Attached document {response.FileName} to a medical claim of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<MedicalClaimDocumentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteMedicalClaimDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteMedicalClaimDocumentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeleteMedicalClaimDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Deletes are manager-only (D-09).
        var (failure, personnelFile) = await LoadForManageMedicalClaimsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var claimInternalId = await employeeRepository.GetMedicalClaimInternalIdAsync(personnelFile.PublicId, command.MedicalClaimPublicId, cancellationToken);
        if (claimInternalId is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetMedicalClaimDocumentEntityAsync(
            command.MedicalClaimPublicId, command.DocumentPublicId, personnelFile.TenantId, cancellationToken);
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
                auditService, personnelFile, $"Deleted document {document.FileName} from a medical claim of {personnelFile.FullName}.", null, cancellationToken);
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
