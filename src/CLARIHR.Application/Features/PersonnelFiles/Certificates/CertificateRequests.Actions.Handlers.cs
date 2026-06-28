using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class ProcessCertificateRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ProcessCertificateRequestCommand, PersonnelFileCertificateRequestResponse>
{
    public async Task<Result<PersonnelFileCertificateRequestResponse>> Handle(
        ProcessCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCertificateRequestsAsync<PersonnelFileCertificateRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var before = await employeeRepository.GetCertificateRequestAsync(personnelFile!.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (before.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!CertificateRequestStatuses.Pending.Contains(before.RequestStatusCode))
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(CertificateRequestErrors.StateRuleViolation);
        }

        var response = await employeeRepository.ProcessCertificateRequestAsync(command.CertificateRequestPublicId, personnelFile.TenantId, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);
        await CommitWithAuditAsync(personnelFile, before, response, $"Marked certificate request in process for {personnelFile.FullName}.", cancellationToken);
        return Result<PersonnelFileCertificateRequestResponse>.Success(response);
    }

    private async Task CommitWithAuditAsync(
        Domain.PersonnelFiles.PersonnelFile personnelFile,
        PersonnelFileCertificateRequestResponse before,
        PersonnelFileCertificateRequestResponse after,
        string message,
        CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, message, before, after, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class IssueCertificateRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICertificatePrintDataProvider printDataProvider,
    ICertificateIssuanceService issuanceService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<IssueCertificateRequestCommand, PersonnelFileCertificateRequestResponse>
{
    public async Task<Result<PersonnelFileCertificateRequestResponse>> Handle(
        IssueCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only (D-04).
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

        var before = await employeeRepository.GetCertificateRequestAsync(personnelFile.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (before.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!CertificateRequestStatuses.Pending.Contains(before.RequestStatusCode))
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(CertificateRequestErrors.StateRuleViolation);
        }

        // (D-20) A salary-printing type requires ViewCompensation in addition to the manage permission.
        if (CertificateRequestRules.PrintsSalary(before.CertificateTypeCode))
        {
            var compensationAccess = await authorizationService.EnsureCanViewCompensationAsync(personnelFile.TenantId, cancellationToken);
            if (compensationAccess.IsFailure)
            {
                return Result<PersonnelFileCertificateRequestResponse>.Failure(CertificateRequestErrors.CompensationForbidden);
            }
        }

        var nowUtc = dateTimeProvider.UtcNow;

        // Build the server-side merge data; null ⇒ a required piece is missing (E-17).
        var payload = await printDataProvider.BuildAsync(personnelFile.PublicId, personnelFile.TenantId, before, nowUtc, cancellationToken);
        if (payload is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(CertificateRequestErrors.GenerationDataUnavailable);
        }

        var internalId = await employeeRepository.GetCertificateRequestInternalIdAsync(personnelFile.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (internalId is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var issuedByUserId);

        // Generate the PDF, upload it and register the issued document (added to the unit of work).
        await issuanceService.GenerateAndStoreAsync(
            personnelFile.TenantId, internalId.Value, command.CertificateRequestPublicId, payload, currentUserService.UserId ?? string.Empty, cancellationToken);

        var response = await employeeRepository.IssueCertificateRequestAsync(
            command.CertificateRequestPublicId, personnelFile.TenantId, issuedByUserId, nowUtc, command.Notes, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Issued certificate for {personnelFile.FullName}.", before, response, cancellationToken);
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

internal sealed class DeliverCertificateRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeliverCertificateRequestCommand, PersonnelFileCertificateRequestResponse>
{
    public async Task<Result<PersonnelFileCertificateRequestResponse>> Handle(
        DeliverCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCertificateRequestsAsync<PersonnelFileCertificateRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var before = await employeeRepository.GetCertificateRequestAsync(personnelFile!.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (before.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an issued certificate can be delivered.
        if (before.RequestStatusCode != CertificateRequestStatuses.Emitida)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(CertificateRequestErrors.StateRuleViolation);
        }

        // Delivery date must not precede the issue date.
        if (before.IssuedDateUtc is { } issuedDate && command.DeliveredDateUtc.Date < issuedDate.Date)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(CertificateRequestErrors.DateIncoherent);
        }

        var response = await employeeRepository.DeliverCertificateRequestAsync(command.CertificateRequestPublicId, personnelFile.TenantId, command.DeliveredDateUtc, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Delivered certificate for {personnelFile.FullName}.", before, response, cancellationToken);
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

internal sealed class RejectCertificateRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RejectCertificateRequestCommand, PersonnelFileCertificateRequestResponse>
{
    public async Task<Result<PersonnelFileCertificateRequestResponse>> Handle(
        RejectCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCertificateRequestsAsync<PersonnelFileCertificateRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var before = await employeeRepository.GetCertificateRequestAsync(personnelFile!.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (before.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!CertificateRequestStatuses.Pending.Contains(before.RequestStatusCode))
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(CertificateRequestErrors.StateRuleViolation);
        }

        var response = await employeeRepository.RejectCertificateRequestAsync(command.CertificateRequestPublicId, personnelFile.TenantId, command.Notes, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Rejected certificate request for {personnelFile.FullName}.", before, response, cancellationToken);
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

internal sealed class CancelCertificateRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<CancelCertificateRequestCommand, PersonnelFileCertificateRequestResponse>
{
    public async Task<Result<PersonnelFileCertificateRequestResponse>> Handle(
        CancelCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        // Self-service (D-02): the owner can cancel their own pending request, or HR (manage).
        var (failure, personnelFile) = await LoadForCreateOwnOrManageCertificateAsync<PersonnelFileCertificateRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var before = await employeeRepository.GetCertificateRequestAsync(personnelFile!.PublicId, command.CertificateRequestPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (before.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!CertificateRequestStatuses.Pending.Contains(before.RequestStatusCode))
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(CertificateRequestErrors.StateRuleViolation);
        }

        var response = await employeeRepository.CancelCertificateRequestAsync(command.CertificateRequestPublicId, personnelFile.TenantId, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileCertificateRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Cancelled certificate request for {personnelFile.FullName}.", before, response, cancellationToken);
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
