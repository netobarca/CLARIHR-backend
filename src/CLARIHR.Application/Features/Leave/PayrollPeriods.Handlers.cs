using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Features.Leave;

internal sealed class SearchPayrollPeriodsQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPayrollPeriodsQuery, PagedResponse<PayrollPeriodListItemResponse>>
{
    public async Task<Result<PagedResponse<PayrollPeriodListItemResponse>>> Handle(
        SearchPayrollPeriodsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PayrollPeriodListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.PayPeriodTypeCode,
            query.Year,
            query.IsActive,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<PayrollPeriodListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => PayrollPeriodPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<PayrollPeriodListItemResponse>>.Success(response);
    }
}

internal sealed class GetPayrollPeriodByIdQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetPayrollPeriodByIdQuery, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        GetPayrollPeriodByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollPeriodResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.PayrollPeriodId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = PayrollPeriodPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<PayrollPeriodResponse>.Success(response);
        }

        return Result<PayrollPeriodResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.PayrollPeriodId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PayrollPeriodErrors.PayrollPeriodNotFound);
    }
}

internal sealed class CreatePayrollPeriodCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePayrollPeriodCommand, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        CreatePayrollPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var businessResult = await PayrollPeriodRules.ValidateAsync(
            repository,
            personnelFileRepository,
            command.CompanyId,
            command.PayPeriodTypeCode,
            command.Year,
            command.Number,
            command.StartDate,
            command.EndDate,
            excludingPayrollPeriodId: null,
            cancellationToken);
        if (businessResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(businessResult.Error);
        }

        PayrollPeriodDefinition payrollPeriod;
        try
        {
            payrollPeriod = PayrollPeriodDefinition.Create(
                command.PayPeriodTypeCode,
                command.Year,
                command.Number,
                command.Label,
                command.StartDate,
                command.EndDate);
        }
        catch (ArgumentException)
        {
            // Validators mirror the domain guards, but a value that still slips through must
            // surface as a clean 422 instead of a 500.
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.RuleViolation);
        }

        payrollPeriod.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(payrollPeriod);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodDefinitionCreated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    payrollPeriod.PublicId,
                    payrollPeriod.Label,
                    AuditActions.Create,
                    $"Created payroll period {payrollPeriod.Label} ({payrollPeriod.PayPeriodTypeCode} {payrollPeriod.Year}-{payrollPeriod.Number}).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (PayrollPeriodConstraintViolations.IsPeriodConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.PeriodConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePayrollPeriodCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePayrollPeriodCommand, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        UpdatePayrollPeriodCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollPeriodResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var payrollPeriod = await repository.GetByIdAsync(command.PayrollPeriodId, cancellationToken);
        if (payrollPeriod is null)
        {
            return Result<PayrollPeriodResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollPeriodId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollPeriodErrors.PayrollPeriodNotFound);
        }

        if (payrollPeriod.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollPeriodResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var businessResult = await PayrollPeriodRules.ValidateAsync(
            repository,
            personnelFileRepository,
            payrollPeriod.TenantId,
            command.PayPeriodTypeCode,
            command.Year,
            command.Number,
            command.StartDate,
            command.EndDate,
            payrollPeriod.PublicId,
            cancellationToken);
        if (businessResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(businessResult.Error);
        }

        var before = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            payrollPeriod.Update(
                command.PayPeriodTypeCode,
                command.Year,
                command.Number,
                command.Label,
                command.StartDate,
                command.EndDate);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodDefinitionUpdated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    payrollPeriod.PublicId,
                    payrollPeriod.Label,
                    AuditActions.Update,
                    $"Updated payroll period {payrollPeriod.Label} ({payrollPeriod.PayPeriodTypeCode} {payrollPeriod.Year}-{payrollPeriod.Number}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Success(after);
        }
        catch (ArgumentException)
        {
            // The domain mutator re-runs the entity guards; a value that slips past the validator
            // surfaces as a clean 422 instead of a 500.
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.RuleViolation);
        }
        catch (UniqueConstraintViolationException ex) when (PayrollPeriodConstraintViolations.IsPeriodConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Failure(PayrollPeriodErrors.PeriodConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivatePayrollPeriodCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePayrollPeriodCommand, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        ActivatePayrollPeriodCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollPeriodResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var payrollPeriod = await repository.GetByIdAsync(command.PayrollPeriodId, cancellationToken);
        if (payrollPeriod is null)
        {
            return Result<PayrollPeriodResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollPeriodId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollPeriodErrors.PayrollPeriodNotFound);
        }

        if (payrollPeriod.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollPeriodResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            payrollPeriod.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodDefinitionActivated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    payrollPeriod.PublicId,
                    payrollPeriod.Label,
                    AuditActions.Reactivate,
                    $"Activated payroll period {payrollPeriod.Label} ({payrollPeriod.PayPeriodTypeCode} {payrollPeriod.Year}-{payrollPeriod.Number}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivatePayrollPeriodCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IPayrollPeriodRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePayrollPeriodCommand, PayrollPeriodResponse>
{
    public async Task<Result<PayrollPeriodResponse>> Handle(
        InactivatePayrollPeriodCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollPeriodResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollPeriodResponse>.Failure(authorizationResult.Error);
        }

        var payrollPeriod = await repository.GetByIdAsync(command.PayrollPeriodId, cancellationToken);
        if (payrollPeriod is null)
        {
            return Result<PayrollPeriodResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollPeriodId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollPeriodErrors.PayrollPeriodNotFound);
        }

        if (payrollPeriod.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollPeriodResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll period response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            payrollPeriod.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(payrollPeriod.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll period response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollPeriodDefinitionInactivated,
                    AuditEntityTypes.PayrollPeriodDefinition,
                    payrollPeriod.PublicId,
                    payrollPeriod.Label,
                    AuditActions.Deactivate,
                    $"Inactivated payroll period {payrollPeriod.Label} ({payrollPeriod.PayPeriodTypeCode} {payrollPeriod.Year}-{payrollPeriod.Number}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollPeriodResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class PayrollPeriodRules
{
    /// <summary>
    /// Shared Create/Update business validations, in order:
    /// (1) the pay-period type must be an active code of the country-scoped pay-periods general
    /// catalog (<c>PAY_PERIOD_CATALOG</c>, validated via CatalogCodeIsActiveAsync like the
    /// compensation-concept pay period) → 422;
    /// (2) (type, year, number) must be unique per tenant → 409 (the probe closes the sequential
    /// case, the unique index closes the concurrent one);
    /// (3) the date range must not overlap another ACTIVE period of the same type and year → 422.
    /// </summary>
    public static async Task<Result> ValidateAsync(
        IPayrollPeriodRepository repository,
        IPersonnelFileRepository personnelFileRepository,
        Guid companyId,
        string payPeriodTypeCode,
        int year,
        int number,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken)
    {
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                companyId,
                PersonnelCurriculumCatalogCategories.PayPeriod,
                payPeriodTypeCode,
                cancellationToken))
        {
            return Result.Failure(PayrollPeriodErrors.TypeInvalid);
        }

        var normalizedTypeCode = payPeriodTypeCode.Trim().ToUpperInvariant();

        if (await repository.PeriodExistsAsync(
                companyId,
                normalizedTypeCode,
                year,
                number,
                excludingPayrollPeriodId,
                cancellationToken))
        {
            return Result.Failure(PayrollPeriodErrors.PeriodConflict);
        }

        if (await repository.HasOverlapAsync(
                companyId,
                normalizedTypeCode,
                year,
                startDate,
                endDate,
                excludingPayrollPeriodId,
                cancellationToken))
        {
            return Result.Failure(PayrollPeriodErrors.PeriodOverlap);
        }

        return Result.Success();
    }
}

internal static class PayrollPeriodConstraintViolations
{
    // The (TenantId, PayPeriodTypeCode, Year, Number) unique index is the real guard against
    // duplicate periods; the up-front PeriodExistsAsync probe only closes the common (sequential)
    // case. On a concurrent create/update of the same period, the second writer trips this index —
    // map it to the same clean 409 as the probe instead of letting the 23505 escape as an HTTP 500
    // (mirrors MedicalClinicConstraintViolations).
    public static bool IsPeriodConflict(string? constraintName) =>
        string.Equals(constraintName, LeaveMasterConstraintNames.PayrollPeriodUnique, StringComparison.Ordinal);
}

internal static class PayrollPeriodPolicyAdapter
{
    public static PayrollPeriodListItemResponse ApplyAllowedActions(
        PayrollPeriodListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.PayrollPeriodsResourceKey,
                response.PayPeriodTypeCode,
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

    public static PayrollPeriodResponse ApplyAllowedActions(
        PayrollPeriodResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.PayrollPeriodsResourceKey,
                response.PayPeriodTypeCode,
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
