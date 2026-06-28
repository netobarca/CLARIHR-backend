using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Resolved snapshot values for an economic-aid write: the type must be an active catalog code (D-04) and its
/// description is snapshotted; the currency defaults from the company preference when omitted and is mandatory.
/// </summary>
internal sealed record EconomicAidResolved(string CurrencyCode, string? TypeName);

internal static class EconomicAidRequestWriteSupport
{
    public static async Task<Result<EconomicAidResolved>> ResolveAndValidateAsync(
        EconomicAidRequestInput input,
        PersonnelFile personnelFile,
        IPersonnelFileRepository personnelFileRepository,
        ICompanyPreferenceRepository companyPreferenceRepository,
        CancellationToken cancellationToken)
    {
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.EconomicAidType, input.TypeCode, cancellationToken))
        {
            return Result<EconomicAidResolved>.Failure(EconomicAidErrors.TypeCodeInvalid);
        }

        var typeName = await personnelFileRepository.GetCatalogItemNameAsync(
            personnelFile.TenantId, PersonnelCurriculumCatalogCategories.EconomicAidType, input.TypeCode, cancellationToken);

        var currency = input.CurrencyCode;
        if (string.IsNullOrWhiteSpace(currency))
        {
            var preference = await companyPreferenceRepository.GetByTenantIdAsync(personnelFile.TenantId, cancellationToken);
            currency = preference?.CurrencyCode;
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result<EconomicAidResolved>.Failure(EconomicAidErrors.CurrencyRequired);
        }

        return Result<EconomicAidResolved>.Success(new EconomicAidResolved(currency.Trim().ToUpperInvariant(), typeName));
    }
}

internal sealed class AddPersonnelFileEconomicAidRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileEconomicAidRequestCommand, PersonnelFileEconomicAidRequestResponse>
{
    public async Task<Result<PersonnelFileEconomicAidRequestResponse>> Handle(
        AddPersonnelFileEconomicAidRequestCommand command,
        CancellationToken cancellationToken)
    {
        // Self-service create (D-02): the employee on their own file, or HR (manage permission).
        var (failure, personnelFile) = await LoadForCreateOwnOrManageEconomicAidAsync<PersonnelFileEconomicAidRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        // Eligibility (D-08): minimum seniority configured at company level (null/0 ⇒ no restriction).
        var preference = await companyPreferenceRepository.GetByTenantIdAsync(personnelFile.TenantId, cancellationToken);
        if (preference?.MinimumSeniorityMonthsForEconomicAid is > 0)
        {
            var profile = await employeeRepository.GetEmployeeProfileAsync(personnelFile.PublicId, cancellationToken);
            if (profile is not null
                && !EconomicAidRequestRules.MeetsMinimumSeniority(profile.HireDate, dateTimeProvider.UtcNow, preference.MinimumSeniorityMonthsForEconomicAid))
            {
                return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.EligibilityNotMet);
            }
        }

        var resolveResult = await EconomicAidRequestWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, companyPreferenceRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;
        _ = Guid.TryParse(currentUserService.UserId, out var requestedByUserId);

        var entity = PersonnelFileEconomicAidRequest.Create(
            command.Item.TypeCode,
            resolved.TypeName,
            command.Item.Description,
            command.Item.RequestedAmount,
            resolved.CurrencyCode,
            command.Item.RequestDateUtc,
            requestedByUserId);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddEconomicAidRequestAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Economic-aid request response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Added economic-aid request for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEconomicAidRequestResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileEconomicAidRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileEconomicAidRequestCommand, PersonnelFileEconomicAidRequestResponse>
{
    public async Task<Result<PersonnelFileEconomicAidRequestResponse>> Handle(
        UpdatePersonnelFileEconomicAidRequestCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only (D-03): editing business fields. Validation/disbursement are separate actions.
        var (failure, personnelFile) = await LoadForManageEconomicAidRequestsAsync<PersonnelFileEconomicAidRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var existing = await employeeRepository.GetEconomicAidRequestAsync(personnelFile.PublicId, command.EconomicAidRequestPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var resolveResult = await EconomicAidRequestWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, companyPreferenceRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;
        var response = await employeeRepository.UpdateEconomicAidRequestAsync(
            command.EconomicAidRequestPublicId, personnelFile.TenantId, command.Item, resolved.CurrencyCode, resolved.TypeName, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated economic-aid request for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEconomicAidRequestResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileEconomicAidRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileEconomicAidRequestCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileEconomicAidRequestCommand command,
        CancellationToken cancellationToken)
    {
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

        var existing = await employeeRepository.GetEconomicAidRequestAsync(personnelFile.PublicId, command.EconomicAidRequestPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Soft delete (RN-10): deactivate, preserving the record for audit/history.
        var removed = await employeeRepository.SoftDeleteEconomicAidRequestAsync(command.EconomicAidRequestPublicId, personnelFile.TenantId, cancellationToken);
        if (!removed)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated economic-aid request for {personnelFile.FullName}.", existing, null, cancellationToken);
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

internal sealed class GetPersonnelFileEconomicAidRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEconomicAidRequestsQuery, IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>>> Handle(
        GetPersonnelFileEconomicAidRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForEconomicAidReadAsync<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEconomicAidRequestsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileEconomicAidRequestByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEconomicAidRequestByIdQuery, PersonnelFileEconomicAidRequestResponse>
{
    public async Task<Result<PersonnelFileEconomicAidRequestResponse>> Handle(
        GetPersonnelFileEconomicAidRequestByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForEconomicAidReadAsync<PersonnelFileEconomicAidRequestResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEconomicAidRequestAsync(personnelFile!.PublicId, query.EconomicAidRequestPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileEconomicAidRequestResponse>.Success(response);
    }
}

internal sealed class ResolveEconomicAidRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ResolveEconomicAidRequestCommand, PersonnelFileEconomicAidRequestResponse>
{
    public async Task<Result<PersonnelFileEconomicAidRequestResponse>> Handle(
        ResolveEconomicAidRequestCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only (D-03): validation is never self-service.
        var (failure, personnelFile) = await LoadForManageEconomicAidRequestsAsync<PersonnelFileEconomicAidRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var before = await employeeRepository.GetEconomicAidRequestAsync(personnelFile.PublicId, command.EconomicAidRequestPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (before.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Anti-self-approval (D-03): the deciding HR user must not be the request's subject employee.
        _ = Guid.TryParse(currentUserService.UserId, out var decidedByUserId);
        if (personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == decidedByUserId)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.SelfApprovalForbidden);
        }

        var targetStatus = command.TargetStatusCode.Trim().ToUpperInvariant();
        if (!EconomicAidRequestRules.IsResolutionTarget(targetStatus)
            || !await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.EconomicAidStatus, targetStatus, cancellationToken))
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.StatusCodeInvalid);
        }

        if (!EconomicAidRequestRules.IsValidApprovedAmount(targetStatus, command.ApprovedAmount))
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.ApprovedAmountInvalid);
        }

        // Transition pre-check: only a pending request can be resolved (avoid a domain exception → 500).
        if (!EconomicAidRequestStatuses.Pending.Contains(before.RequestStatusCode))
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.StateRuleViolation);
        }

        var response = await employeeRepository.ResolveEconomicAidRequestAsync(
            command.EconomicAidRequestPublicId, personnelFile.TenantId, targetStatus, command.ApprovedAmount,
            decidedByUserId, dateTimeProvider.UtcNow, command.Notes, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Validated economic-aid request for {personnelFile.FullName}.", before, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEconomicAidRequestResponse>.Success(response);
    }
}

internal sealed class DisburseEconomicAidRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DisburseEconomicAidRequestCommand, PersonnelFileEconomicAidRequestResponse>
{
    public async Task<Result<PersonnelFileEconomicAidRequestResponse>> Handle(
        DisburseEconomicAidRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageEconomicAidRequestsAsync<PersonnelFileEconomicAidRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var before = await employeeRepository.GetEconomicAidRequestAsync(personnelFile.PublicId, command.EconomicAidRequestPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (before.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an approved request can be disbursed (avoid a domain exception → 500).
        if (before.RequestStatusCode != EconomicAidRequestStatuses.Aprobada)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.StateRuleViolation);
        }

        // Date coherence: the disbursement cannot precede the resolution (D-09 / RN-07).
        if (before.ResolutionDateUtc is { } resolutionDate && command.DisbursementDateUtc.Date < resolutionDate.Date)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.DateIncoherent);
        }

        // Optional payment method must be an active catalog code.
        if (!string.IsNullOrWhiteSpace(command.PaymentMethodCode)
            && !await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PaymentMethod, command.PaymentMethodCode, cancellationToken))
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.PaymentMethodInvalid);
        }

        var response = await employeeRepository.DisburseEconomicAidRequestAsync(
            command.EconomicAidRequestPublicId, personnelFile.TenantId, command.DisbursedAmount, command.DisbursementDateUtc, command.PaymentMethodCode, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Disbursed economic-aid request for {personnelFile.FullName}.", before, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEconomicAidRequestResponse>.Success(response);
    }
}

internal sealed class CancelEconomicAidRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<CancelEconomicAidRequestCommand, PersonnelFileEconomicAidRequestResponse>
{
    public async Task<Result<PersonnelFileEconomicAidRequestResponse>> Handle(
        CancelEconomicAidRequestCommand command,
        CancellationToken cancellationToken)
    {
        // Self-service (D-11): the owner can cancel their own pending request, or HR (manage).
        var (failure, personnelFile) = await LoadForCreateOwnOrManageEconomicAidAsync<PersonnelFileEconomicAidRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var before = await employeeRepository.GetEconomicAidRequestAsync(personnelFile.PublicId, command.EconomicAidRequestPublicId, cancellationToken);
        if (before is null)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (before.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only a pending request can be canceled (avoid a domain exception → 500).
        if (!EconomicAidRequestStatuses.Pending.Contains(before.RequestStatusCode))
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(EconomicAidErrors.StateRuleViolation);
        }

        var response = await employeeRepository.CancelEconomicAidRequestAsync(
            command.EconomicAidRequestPublicId, personnelFile.TenantId, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileEconomicAidRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Canceled economic-aid request for {personnelFile.FullName}.", before, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEconomicAidRequestResponse>.Success(response);
    }
}
