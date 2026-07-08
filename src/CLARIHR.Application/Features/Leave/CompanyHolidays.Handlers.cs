using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Features.Leave;

internal sealed class SearchCompanyHolidaysQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompanyHolidayRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchCompanyHolidaysQuery, PagedResponse<CompanyHolidayListItemResponse>>
{
    public async Task<Result<PagedResponse<CompanyHolidayListItemResponse>>> Handle(
        SearchCompanyHolidaysQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CompanyHolidayListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.Year,
            query.ScopeCode,
            query.IsActive,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<CompanyHolidayListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => CompanyHolidayPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<CompanyHolidayListItemResponse>>.Success(response);
    }
}

internal sealed class GetCompanyHolidayByIdQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompanyHolidayRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetCompanyHolidayByIdQuery, CompanyHolidayResponse>
{
    public async Task<Result<CompanyHolidayResponse>> Handle(
        GetCompanyHolidayByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompanyHolidayResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyHolidayResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.CompanyHolidayId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CompanyHolidayPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<CompanyHolidayResponse>.Success(response);
        }

        return Result<CompanyHolidayResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.CompanyHolidayId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompanyHolidayErrors.CompanyHolidayNotFound);
    }
}

internal sealed class CreateCompanyHolidayCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompanyHolidayRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCompanyHolidayCommand, CompanyHolidayResponse>
{
    public async Task<Result<CompanyHolidayResponse>> Handle(
        CreateCompanyHolidayCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyHolidayResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.DateExistsAsync(
                command.CompanyId,
                command.Date,
                excludingCompanyHolidayId: null,
                cancellationToken))
        {
            return Result<CompanyHolidayResponse>.Failure(CompanyHolidayErrors.DateConflict);
        }

        CompanyHoliday companyHoliday;
        try
        {
            companyHoliday = CompanyHoliday.Create(
                command.Date,
                command.Description,
                command.ScopeCode);
        }
        catch (ArgumentException)
        {
            // Validators mirror the domain guards, but a value that still slips through (e.g. an
            // unsupported scope code) must surface as a clean 422 instead of a 500.
            return Result<CompanyHolidayResponse>.Failure(CompanyHolidayErrors.RuleViolation);
        }

        companyHoliday.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(companyHoliday);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(companyHoliday.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Company holiday response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanyHolidayCreated,
                    AuditEntityTypes.CompanyHoliday,
                    companyHoliday.PublicId,
                    companyHoliday.Description,
                    AuditActions.Create,
                    $"Created company holiday {companyHoliday.Description} ({companyHoliday.Date:yyyy-MM-dd}).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyHolidayResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (CompanyHolidayConstraintViolations.IsDateConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CompanyHolidayResponse>.Failure(CompanyHolidayErrors.DateConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompanyHolidayCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompanyHolidayRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompanyHolidayCommand, CompanyHolidayResponse>
{
    public async Task<Result<CompanyHolidayResponse>> Handle(
        UpdateCompanyHolidayCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompanyHolidayResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyHolidayResponse>.Failure(authorizationResult.Error);
        }

        var companyHoliday = await repository.GetByIdAsync(command.CompanyHolidayId, cancellationToken);
        if (companyHoliday is null)
        {
            return Result<CompanyHolidayResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CompanyHolidayId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompanyHolidayErrors.CompanyHolidayNotFound);
        }

        if (companyHoliday.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyHolidayResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        if (await repository.DateExistsAsync(
                companyHoliday.TenantId,
                command.Date,
                companyHoliday.PublicId,
                cancellationToken))
        {
            return Result<CompanyHolidayResponse>.Failure(CompanyHolidayErrors.DateConflict);
        }

        var before = await repository.GetResponseByIdAsync(companyHoliday.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Company holiday response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            companyHoliday.Update(
                command.Date,
                command.Description,
                command.ScopeCode);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(companyHoliday.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Company holiday response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanyHolidayUpdated,
                    AuditEntityTypes.CompanyHoliday,
                    companyHoliday.PublicId,
                    companyHoliday.Description,
                    AuditActions.Update,
                    $"Updated company holiday {companyHoliday.Description} ({companyHoliday.Date:yyyy-MM-dd}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyHolidayResponse>.Success(after);
        }
        catch (ArgumentException)
        {
            // The domain mutator re-runs the entity guards; a value that slips past the validator
            // (e.g. an unsupported scope code) surfaces as a clean 422 instead of a 500.
            await transaction.RollbackAsync(cancellationToken);
            return Result<CompanyHolidayResponse>.Failure(CompanyHolidayErrors.RuleViolation);
        }
        catch (UniqueConstraintViolationException ex) when (CompanyHolidayConstraintViolations.IsDateConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CompanyHolidayResponse>.Failure(CompanyHolidayErrors.DateConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCompanyHolidayCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompanyHolidayRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateCompanyHolidayCommand, CompanyHolidayResponse>
{
    public async Task<Result<CompanyHolidayResponse>> Handle(
        ActivateCompanyHolidayCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompanyHolidayResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyHolidayResponse>.Failure(authorizationResult.Error);
        }

        var companyHoliday = await repository.GetByIdAsync(command.CompanyHolidayId, cancellationToken);
        if (companyHoliday is null)
        {
            return Result<CompanyHolidayResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CompanyHolidayId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompanyHolidayErrors.CompanyHolidayNotFound);
        }

        if (companyHoliday.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyHolidayResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(companyHoliday.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Company holiday response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            companyHoliday.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(companyHoliday.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Company holiday response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanyHolidayActivated,
                    AuditEntityTypes.CompanyHoliday,
                    companyHoliday.PublicId,
                    companyHoliday.Description,
                    AuditActions.Reactivate,
                    $"Activated company holiday {companyHoliday.Description} ({companyHoliday.Date:yyyy-MM-dd}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyHolidayResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCompanyHolidayCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ICompanyHolidayRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateCompanyHolidayCommand, CompanyHolidayResponse>
{
    public async Task<Result<CompanyHolidayResponse>> Handle(
        InactivateCompanyHolidayCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompanyHolidayResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyHolidayResponse>.Failure(authorizationResult.Error);
        }

        var companyHoliday = await repository.GetByIdAsync(command.CompanyHolidayId, cancellationToken);
        if (companyHoliday is null)
        {
            return Result<CompanyHolidayResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.CompanyHolidayId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompanyHolidayErrors.CompanyHolidayNotFound);
        }

        if (companyHoliday.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyHolidayResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(companyHoliday.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Company holiday response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            companyHoliday.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(companyHoliday.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Company holiday response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanyHolidayInactivated,
                    AuditEntityTypes.CompanyHoliday,
                    companyHoliday.PublicId,
                    companyHoliday.Description,
                    AuditActions.Deactivate,
                    $"Inactivated company holiday {companyHoliday.Description} ({companyHoliday.Date:yyyy-MM-dd}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyHolidayResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CompanyHolidayConstraintViolations
{
    // The (TenantId, Date) unique index is the real guard against duplicate holiday dates; the
    // up-front DateExistsAsync probe only closes the common (sequential) case. On a concurrent
    // create/update of the same date, the second writer trips this index — map it to the same
    // clean 409 as the probe instead of letting the 23505 escape as an HTTP 500 (mirrors
    // MedicalClinicConstraintViolations).
    public static bool IsDateConflict(string? constraintName) =>
        string.Equals(constraintName, LeaveMasterConstraintNames.CompanyHolidayDateUnique, StringComparison.Ordinal);
}

internal static class CompanyHolidayPolicyAdapter
{
    public static CompanyHolidayListItemResponse ApplyAllowedActions(
        CompanyHolidayListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.CompanyHolidaysResourceKey,
                response.ScopeCode,
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static CompanyHolidayResponse ApplyAllowedActions(
        CompanyHolidayResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.CompanyHolidaysResourceKey,
                response.ScopeCode,
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }
}
