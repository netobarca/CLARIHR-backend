using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Resolved snapshot values for a certificate write: the snapshotted type description (D-14).</summary>
internal sealed record CertificateResolved(string? TypeName);

internal static class CertificateRequestWriteSupport
{
    /// <summary>
    /// Validates the type/purpose/delivery-method catalog codes (422), enforces the embassy-addressee rule (D-06)
    /// and snapshots the type description. Catalog validity is database-backed, so it lives here (not in the
    /// pure rules module).
    /// </summary>
    public static async Task<Result<CertificateResolved>> ResolveAndValidateAsync(
        CertificateRequestInput input,
        PersonnelFile personnelFile,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.CertificateType, input.TypeCode, cancellationToken))
        {
            return Result<CertificateResolved>.Failure(CertificateRequestErrors.TypeCodeInvalid);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.CertificatePurpose, input.PurposeCode, cancellationToken))
        {
            return Result<CertificateResolved>.Failure(CertificateRequestErrors.PurposeCodeInvalid);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.CertificateDeliveryMethod, input.DeliveryMethodCode, cancellationToken))
        {
            return Result<CertificateResolved>.Failure(CertificateRequestErrors.DeliveryMethodCodeInvalid);
        }

        // Embassy certificates must carry an addressee ("dirigida a") — D-06.
        if (CertificateRequestRules.RequiresAddressee(input.TypeCode) && string.IsNullOrWhiteSpace(input.AddressedTo))
        {
            return Result<CertificateResolved>.Failure(CertificateRequestErrors.AddresseeRequired);
        }

        var typeName = await personnelFileRepository.GetCatalogItemNameAsync(
            personnelFile.TenantId, PersonnelCurriculumCatalogCategories.CertificateType, input.TypeCode, cancellationToken);

        return Result<CertificateResolved>.Success(new CertificateResolved(typeName));
    }
}

internal sealed class AddPersonnelFileCertificateRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileCertificateRequestCommand, PersonnelFileCertificateRequestResponse>
{
    public async Task<Result<PersonnelFileCertificateRequestResponse>> Handle(
        AddPersonnelFileCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        // Self-service create (D-02): the employee on their own file, or HR (manage permission).
        var (failure, personnelFile) = await LoadForCreateOwnOrManageCertificateAsync<PersonnelFileCertificateRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var resolveResult = await CertificateRequestWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(resolveResult.Error);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var requestedByUserId);

        var entity = PersonnelFileCertificateRequest.Create(
            command.Item.TypeCode,
            resolveResult.Value.TypeName,
            command.Item.PurposeCode,
            command.Item.AddressedTo,
            command.Item.DeliveryMethodCode,
            command.Item.LanguageCode ?? "es",
            command.Item.Copies ?? 1,
            command.Item.RequestDateUtc,
            command.Item.NeededByDateUtc,
            requestedByUserId);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddCertificateRequestAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Certificate request response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added certificate request for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileCertificateRequestResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileCertificateRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileCertificateRequestCommand, PersonnelFileCertificateRequestResponse>
{
    public async Task<Result<PersonnelFileCertificateRequestResponse>> Handle(
        UpdatePersonnelFileCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only (D-04): editing business fields. Issuance/delivery are separate actions.
        var (failure, personnelFile) = await LoadForManageCertificateRequestsAsync<PersonnelFileCertificateRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetCertificateRequestAsync(personnelFile.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var resolveResult = await CertificateRequestWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(resolveResult.Error);
        }

        var response = await employeeRepository.UpdateCertificateRequestAsync(
            command.CertificateRequestPublicId, personnelFile.TenantId, command.Item, resolveResult.Value.TypeName, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated certificate request for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileCertificateRequestResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileCertificateRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileCertificateRequestCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileCertificateRequestCommand command,
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

        var existing = await employeeRepository.GetCertificateRequestAsync(personnelFile.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Soft delete (RN-08): deactivate, preserving the record for audit/history.
        var removed = await employeeRepository.SoftDeleteCertificateRequestAsync(command.CertificateRequestPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated certificate request for {personnelFile.FullName}.", existing, null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileParentConcurrencyResult>.Success(new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
    }
}

internal sealed class GetPersonnelFileCertificateRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileCertificateRequestsQuery, IReadOnlyCollection<PersonnelFileCertificateRequestResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>>> Handle(
        GetPersonnelFileCertificateRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCertificateReadAsync<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetCertificateRequestsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileCertificateRequestByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileCertificateRequestByIdQuery, PersonnelFileCertificateRequestResponse>
{
    public async Task<Result<PersonnelFileCertificateRequestResponse>> Handle(
        GetPersonnelFileCertificateRequestByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCertificateReadAsync<PersonnelFileCertificateRequestResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetCertificateRequestAsync(personnelFile!.PublicId, query.CertificateRequestPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileCertificateRequestResponse>.Success(response);
    }
}
