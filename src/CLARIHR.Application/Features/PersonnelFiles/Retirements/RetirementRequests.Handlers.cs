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

/// <summary>
/// Resolved snapshot values for a retirement-request write: the retirement category/reason must be active,
/// hierarchy-coherent catalog codes (their names are snapshotted), the requester must reference a valid active
/// personnel file of the company (its display name is snapshotted — D-02), and the dates must be coherent
/// (RN-001.4/RF-016, UTC-date semantics).
/// </summary>
internal sealed record RetirementRequestResolved(
    string RequesterName,
    string? CategoryName,
    string? ReasonName);

internal static class RetirementRequestWriteSupport
{
    public static async Task<Result<RetirementRequestResolved>> ResolveAndValidateAsync(
        RetirementRequestInput input,
        PersonnelFile personnelFile,
        DateTime hireDate,
        DateTime asOfUtc,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        if (!RetirementRequestRules.AreDatesCoherent(input.RequestDate, input.RetirementDate, hireDate, asOfUtc))
        {
            return Result<RetirementRequestResolved>.Failure(RetirementErrors.DateIncoherent);
        }

        var requester = await employeeRepository.GetRetirementRequesterLookupAsync(
            input.RequesterFilePublicId, personnelFile.TenantId, cancellationToken);
        if (requester is null || !requester.IsActive)
        {
            return Result<RetirementRequestResolved>.Failure(RetirementErrors.RequesterInvalid);
        }

        // Category + reason: active for the country AND the reason belongs to the category (existing
        // hierarchical validation of the retirement reference catalogs).
        var catalogError = await PersonnelReferenceCatalogValidation.ValidateRetirementCodesAsync(
            personnelFileRepository, personnelFile.TenantId, input.RetirementCategoryCode, input.RetirementReasonCode, cancellationToken);
        if (catalogError != Error.None)
        {
            return Result<RetirementRequestResolved>.Failure(catalogError);
        }

        var (categoryName, reasonName) = await personnelFileRepository.GetRetirementCatalogNamesAsync(
            personnelFile.TenantId, input.RetirementCategoryCode, input.RetirementReasonCode, cancellationToken);

        return Result<RetirementRequestResolved>.Success(
            new RetirementRequestResolved(requester.FullName, categoryName, reasonName));
    }
}

internal sealed class AddPersonnelFileRetirementRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileRetirementRequestCommand, PersonnelFileRetirementRequestResponse>
{
    public async Task<Result<PersonnelFileRetirementRequestResponse>> Handle(
        AddPersonnelFileRetirementRequestCommand command,
        CancellationToken cancellationToken)
    {
        // HR-only (D-03): registering the baja request is never self-service in Fase 1.
        var (failure, personnelFile) = await LoadForManageRetirementsAsync<PersonnelFileRetirementRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Eligibility (RN-001.1): Employee + Completed, active file, no retirement in force on the profile.
        var profile = await employeeRepository.GetEmployeeProfileAsync(personnelFile!.PublicId, cancellationToken);
        if (profile is null
            || !RetirementRequestRules.IsEligibleForRequest(personnelFile.IsCompletedEmployee, personnelFile.IsActive, profile.RetirementDate))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.EmployeeNotEligible);
        }

        // RN-001.2: at most one open request per employee (the filtered unique index is the DB backstop).
        if (await employeeRepository.HasOpenRetirementRequestAsync(personnelFile.Id, personnelFile.TenantId, cancellationToken))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.RequestAlreadyOpen);
        }

        var resolveResult = await RetirementRequestWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, profile.HireDate, dateTimeProvider.UtcNow, personnelFileRepository, employeeRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;
        _ = Guid.TryParse(currentUserService.UserId, out var requestedByUserId);

        var entity = PersonnelFileRetirementRequest.Create(
            command.Item.RequesterFilePublicId,
            resolved.RequesterName,
            command.Item.RequestDate,
            command.Item.RetirementDate,
            command.Item.RetirementCategoryCode,
            resolved.CategoryName,
            command.Item.RetirementReasonCode,
            resolved.ReasonName,
            command.Item.Notes,
            requestedByUserId);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddRetirementRequestAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Retirement request response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Registered retirement request for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileRetirementRequestResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileRetirementRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileRetirementRequestCommand, PersonnelFileRetirementRequestResponse>
{
    public async Task<Result<PersonnelFileRetirementRequestResponse>> Handle(
        UpdatePersonnelFileRetirementRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRetirementsAsync<PersonnelFileRetirementRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var existing = await employeeRepository.GetRetirementRequestAsync(personnelFile!.PublicId, command.RetirementRequestPublicId, cancellationToken);
        if (existing is null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // RN-003.1: only SOLICITADA is editable (an AUTORIZADA is annulled and re-registered).
        if (existing.RequestStatusCode != RetirementRequestStatuses.Solicitada)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.StateRuleViolation);
        }

        var profile = await employeeRepository.GetEmployeeProfileAsync(personnelFile.PublicId, cancellationToken);
        if (profile is null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.EmployeeNotEligible);
        }

        var resolveResult = await RetirementRequestWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, profile.HireDate, dateTimeProvider.UtcNow, personnelFileRepository, employeeRepository, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(resolveResult.Error);
        }

        var resolved = resolveResult.Value;
        var response = await employeeRepository.UpdateRetirementRequestAsync(
            command.RetirementRequestPublicId, personnelFile.TenantId, command.Item, resolved.RequesterName, resolved.CategoryName, resolved.ReasonName, cancellationToken);
        if (response is null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated retirement request for {personnelFile.FullName}.", existing, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileRetirementRequestResponse>.Success(response);
    }
}

internal sealed class CancelRetirementRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<CancelRetirementRequestCommand, PersonnelFileRetirementRequestResponse>
{
    public async Task<Result<PersonnelFileRetirementRequestResponse>> Handle(
        CancelRetirementRequestCommand command,
        CancellationToken cancellationToken)
    {
        // Manager annulment of a SOLICITADA (RN-005.1). An AUTORIZADA is annulled by the AUTHORIZER through
        // PATCH …/annulment on the resolution controller; an EJECUTADA is never annulled — it is reverted.
        var (failure, personnelFile) = await LoadForManageRetirementsAsync<PersonnelFileRetirementRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetRetirementRequestEntityAsync(
            personnelFile!.PublicId, command.RetirementRequestPublicId, personnelFile.TenantId, includeClosedRecords: false, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.RequestStatusCode != RetirementRequestStatuses.Solicitada)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var canceledByUserId);
        entity.Cancel(canceledByUserId, dateTimeProvider.UtcNow, command.Notes);
        var response = RetirementRequestMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Canceled retirement request for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileRetirementRequestResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileRetirementRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileRetirementRequestsQuery, IReadOnlyCollection<PersonnelFileRetirementRequestResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>>> Handle(
        GetPersonnelFileRetirementRequestsQuery query,
        CancellationToken cancellationToken)
    {
        // RRHH-only read (D-12): no self-service in Fase 1.
        var (failure, personnelFile) = await LoadForViewRetirementsAsync<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var items = await employeeRepository.GetRetirementRequestsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>>.Success(items);
    }
}

internal sealed class GetPersonnelFileRetirementRequestByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileRetirementRequestByIdQuery, PersonnelFileRetirementRequestResponse>
{
    public async Task<Result<PersonnelFileRetirementRequestResponse>> Handle(
        GetPersonnelFileRetirementRequestByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForViewRetirementsAsync<PersonnelFileRetirementRequestResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var item = await employeeRepository.GetRetirementRequestAsync(personnelFile!.PublicId, query.RetirementRequestPublicId, cancellationToken);
        return item is null
            ? Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileRetirementRequestResponse>.Success(item);
    }
}
