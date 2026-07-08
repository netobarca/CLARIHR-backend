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

internal sealed class GetCompensatoryTimeCreditDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeCreditDocumentsQuery, IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>
{
    public async Task<Result<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>> Handle(
        GetCompensatoryTimeCreditDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var creditInternalId = await compensatoryTimeRepository.GetCreditInternalIdAsync(personnelFile!.PublicId, query.CompensatoryTimeCreditPublicId, cancellationToken);
        if (creditInternalId is null)
        {
            return Result<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await compensatoryTimeRepository.GetDocumentResponsesAsync(query.CompensatoryTimeCreditPublicId, cancellationToken);
        return Result<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>.Success(response);
    }
}

internal sealed class GetCompensatoryTimeCreditDocumentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeCreditDocumentByIdQuery, CompensatoryTimeCreditDocumentResponse>
{
    public async Task<Result<CompensatoryTimeCreditDocumentResponse>> Handle(
        GetCompensatoryTimeCreditDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<CompensatoryTimeCreditDocumentResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var creditInternalId = await compensatoryTimeRepository.GetCreditInternalIdAsync(personnelFile!.PublicId, query.CompensatoryTimeCreditPublicId, cancellationToken);
        if (creditInternalId is null)
        {
            return Result<CompensatoryTimeCreditDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await compensatoryTimeRepository.GetDocumentResponseAsync(query.CompensatoryTimeCreditPublicId, query.DocumentPublicId, cancellationToken);
        return document is null
            ? Result<CompensatoryTimeCreditDocumentResponse>.Failure(PersonnelFileErrors.DocumentNotFound)
            : Result<CompensatoryTimeCreditDocumentResponse>.Success(document);
    }
}

internal sealed class GetCompensatoryTimeCreditDocumentReadUrlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeCreditDocumentReadUrlQuery, GetCompensatoryTimeCreditDocumentReadUrlResponse>
{
    public async Task<Result<GetCompensatoryTimeCreditDocumentReadUrlResponse>> Handle(
        GetCompensatoryTimeCreditDocumentReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<GetCompensatoryTimeCreditDocumentReadUrlResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var creditInternalId = await compensatoryTimeRepository.GetCreditInternalIdAsync(personnelFile!.PublicId, query.CompensatoryTimeCreditPublicId, cancellationToken);
        if (creditInternalId is null)
        {
            return Result<GetCompensatoryTimeCreditDocumentReadUrlResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await compensatoryTimeRepository.GetDocumentResponseAsync(query.CompensatoryTimeCreditPublicId, query.DocumentPublicId, cancellationToken);
        if (document is null)
        {
            return Result<GetCompensatoryTimeCreditDocumentReadUrlResponse>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        var file = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetCompensatoryTimeCreditDocumentReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetCompensatoryTimeCreditDocumentReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetCompensatoryTimeCreditDocumentReadUrlResponse>.Success(
            new GetCompensatoryTimeCreditDocumentReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}

internal sealed class AddCompensatoryTimeCreditDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddCompensatoryTimeCreditDocumentCommand, CompensatoryTimeCreditDocumentResponse>
{
    public async Task<Result<CompensatoryTimeCreditDocumentResponse>> Handle(
        AddCompensatoryTimeCreditDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Compensatory time is HR-only (D-01): attaching a document is a manage operation, no self-service.
        var (failure, personnelFile) = await LoadForManageCompensatoryTimeAsync<CompensatoryTimeCreditDocumentResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<CompensatoryTimeCreditDocumentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await compensatoryTimeRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<CompensatoryTimeCreditDocumentResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var creditInternalId = await compensatoryTimeRepository.GetCreditInternalIdAsync(personnelFile.PublicId, command.CompensatoryTimeCreditPublicId, cancellationToken);
        if (creditInternalId is null)
        {
            return Result<CompensatoryTimeCreditDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<CompensatoryTimeCreditDocumentResponse>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<CompensatoryTimeCreditDocumentResponse>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantContext.TenantId!.Value)
        {
            return Result<CompensatoryTimeCreditDocumentResponse>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.CompensatoryTimeDocument)
        {
            return Result<CompensatoryTimeCreditDocumentResponse>.Failure(CompensatoryTimeCreditErrors.DocumentPurposeInvalid);
        }

        long? documentTypeInternalId = null;
        if (command.DocumentTypeCatalogItemPublicId is { } documentTypePublicId)
        {
            var lookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(documentTypePublicId, cancellationToken);
            if (lookup is null)
            {
                return Result<CompensatoryTimeCreditDocumentResponse>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                    }));
            }

            documentTypeInternalId = lookup.InternalId;
        }

        var entity = PersonnelFileCompensatoryTimeCreditDocument.Create(
            Guid.NewGuid(),
            documentTypeInternalId,
            storedFile.PublicId,
            storedFile.FileName,
            storedFile.ContentType,
            (int)storedFile.SizeBytes,
            command.Observations);
        entity.BindToCredit(creditInternalId.Value);
        entity.SetTenantId(personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            compensatoryTimeRepository.AddDocument(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await compensatoryTimeRepository.GetDocumentResponseAsync(command.CompensatoryTimeCreditPublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time credit document could not be resolved after upload.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Attached document {response.FileName} to a compensatory-time credit of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<CompensatoryTimeCreditDocumentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteCompensatoryTimeCreditDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteCompensatoryTimeCreditDocumentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeleteCompensatoryTimeCreditDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompensatoryTimeAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (await compensatoryTimeRepository.IsProfileRetiredAsync(personnelFile!.Id, cancellationToken))
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var creditInternalId = await compensatoryTimeRepository.GetCreditInternalIdAsync(personnelFile.PublicId, command.CompensatoryTimeCreditPublicId, cancellationToken);
        if (creditInternalId is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await compensatoryTimeRepository.GetDocumentEntityAsync(
            command.CompensatoryTimeCreditPublicId, command.DocumentPublicId, personnelFile.TenantId, cancellationToken);
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
                auditService, personnelFile, $"Deleted document {document.FileName} from a compensatory-time credit of {personnelFile.FullName}.", null, cancellationToken);
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
