using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
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

internal sealed class GetCertificateRequestDocumentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCertificateRequestDocumentsQuery, IReadOnlyCollection<CertificateRequestDocumentResponse>>
{
    public async Task<Result<IReadOnlyCollection<CertificateRequestDocumentResponse>>> Handle(
        GetCertificateRequestDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCertificateReadAsync<IReadOnlyCollection<CertificateRequestDocumentResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var requestInternalId = await employeeRepository.GetCertificateRequestInternalIdAsync(personnelFile!.PublicId, query.CertificateRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<IReadOnlyCollection<CertificateRequestDocumentResponse>>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var response = await employeeRepository.GetCertificateRequestDocumentsAsync(query.CertificateRequestPublicId, cancellationToken);
        return Result<IReadOnlyCollection<CertificateRequestDocumentResponse>>.Success(response);
    }
}

internal sealed class GetCertificateRequestDocumentByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCertificateRequestDocumentByIdQuery, CertificateRequestDocumentResponse>
{
    public async Task<Result<CertificateRequestDocumentResponse>> Handle(
        GetCertificateRequestDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCertificateReadAsync<CertificateRequestDocumentResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var requestInternalId = await employeeRepository.GetCertificateRequestInternalIdAsync(personnelFile!.PublicId, query.CertificateRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<CertificateRequestDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetCertificateRequestDocumentAsync(query.CertificateRequestPublicId, query.DocumentPublicId, cancellationToken);
        return document is null
            ? Result<CertificateRequestDocumentResponse>.Failure(PersonnelFileErrors.DocumentNotFound)
            : Result<CertificateRequestDocumentResponse>.Success(document);
    }
}

internal sealed class GetCertificateRequestDocumentReadUrlQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IFileRepository fileRepository,
    IFileStorageProviderResolver providerResolver,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCertificateRequestDocumentReadUrlQuery, GetCertificateRequestDocumentReadUrlResponse>
{
    public async Task<Result<GetCertificateRequestDocumentReadUrlResponse>> Handle(
        GetCertificateRequestDocumentReadUrlQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCertificateReadAsync<GetCertificateRequestDocumentReadUrlResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var requestInternalId = await employeeRepository.GetCertificateRequestInternalIdAsync(personnelFile!.PublicId, query.CertificateRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<GetCertificateRequestDocumentReadUrlResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetCertificateRequestDocumentAsync(query.CertificateRequestPublicId, query.DocumentPublicId, cancellationToken);
        if (document is null)
        {
            return Result<GetCertificateRequestDocumentReadUrlResponse>.Failure(PersonnelFileErrors.DocumentNotFound);
        }

        var file = await fileRepository.GetByPublicIdAsync(document.FilePublicId, cancellationToken);
        if (file is null)
        {
            return Result<GetCertificateRequestDocumentReadUrlResponse>.Failure(FileErrors.FileNotFound);
        }

        if (file.Status != FileStatus.Active)
        {
            return Result<GetCertificateRequestDocumentReadUrlResponse>.Failure(FileErrors.FileNotActive);
        }

        var provider = providerResolver.Resolve(file.Provider);
        var session = await provider.CreateReadSessionAsync(
            new CreateReadSessionCommand(file.ContainerName, file.ObjectKey),
            cancellationToken);

        return Result<GetCertificateRequestDocumentReadUrlResponse>.Success(
            new GetCertificateRequestDocumentReadUrlResponse(session.ReadUrl, session.ExpiresUtc));
    }
}

internal sealed class AddCertificateRequestDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddCertificateRequestDocumentCommand, CertificateRequestDocumentResponse>
{
    public async Task<Result<CertificateRequestDocumentResponse>> Handle(
        AddCertificateRequestDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only (D-05/RN-13): the manual override is produced by HR, not the employee.
        var (failure, personnelFile) = await LoadForManageCertificateRequestsAsync<CertificateRequestDocumentResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<CertificateRequestDocumentResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var requestInternalId = await employeeRepository.GetCertificateRequestInternalIdAsync(personnelFile.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<CertificateRequestDocumentResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(command.FilePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<CertificateRequestDocumentResponse>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<CertificateRequestDocumentResponse>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantContext.TenantId!.Value)
        {
            return Result<CertificateRequestDocumentResponse>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.CertificateRequestDocument)
        {
            return Result<CertificateRequestDocumentResponse>.Failure(FileErrors.InvalidPurpose(storedFile.Purpose.ToString()));
        }

        var entity = CertificateRequestDocument.Create(
            Guid.NewGuid(),
            isSystemGenerated: false,
            storedFile.PublicId,
            storedFile.FileName,
            storedFile.ContentType,
            (int)storedFile.SizeBytes,
            command.Observations);
        entity.BindToCertificateRequest(requestInternalId.Value);
        entity.SetTenantId(personnelFile.TenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AddCertificateRequestDocumentAsync(entity, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await employeeRepository.GetCertificateRequestDocumentAsync(command.CertificateRequestPublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Certificate request document could not be resolved after upload.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Attached document {response.FileName} to a certificate request of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<CertificateRequestDocumentResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeleteCertificateRequestDocumentCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IFileRepository fileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeleteCertificateRequestDocumentCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeleteCertificateRequestDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCertificateRequestsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var requestInternalId = await employeeRepository.GetCertificateRequestInternalIdAsync(personnelFile.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (requestInternalId is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        var document = await employeeRepository.GetCertificateRequestDocumentEntityAsync(
            command.CertificateRequestPublicId, command.DocumentPublicId, personnelFile.TenantId, cancellationToken);
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
                auditService, personnelFile, $"Deleted document {document.FileName} from a certificate request of {personnelFile.FullName}.", null, cancellationToken);
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
